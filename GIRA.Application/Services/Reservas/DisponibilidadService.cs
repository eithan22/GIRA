using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Implementación de <see cref="IDisponibilidadService"/>: calcula qué franjas tienen al
/// menos una mesa disponible para una fecha y cantidad de personas.
/// </summary>
public class DisponibilidadService : IDisponibilidadService
{
    private readonly IMesaRepository _mesaRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IParametrosReservaService _parametros;
    private readonly IValidator<ConsultarDisponibilidadRequest> _validator;
    private readonly TimeProvider _timeProvider;

    /// <summary>Crea el servicio con sus repositorios, los parámetros del restaurante y el reloj.</summary>
    public DisponibilidadService(
        IMesaRepository mesaRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IReservaRepository reservaRepository,
        IParametrosReservaService parametros,
        IValidator<ConsultarDisponibilidadRequest> validator,
        TimeProvider timeProvider)
    {
        _mesaRepository = mesaRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _reservaRepository = reservaRepository;
        _parametros = parametros;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarAsync(
        ConsultarDisponibilidadRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                ErrorType.Validation, validacion.ToString());
        }

        var mesas = await _mesaRepository.ListarActivasAsync(cancellationToken);
        if (mesas.Count == 0)
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        var capacidadMaxima = mesas.Max(m => m.Capacidad);
        if (request.Personas > capacidadMaxima)
        {
            return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                ErrorType.Validation,
                $"Para grupos de más de {capacidadMaxima} personas, llama al restaurante.");
        }

        if (_parametros.DiasCerrados.Contains(request.Fecha.DayOfWeek))
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        var mesasConCapacidad = mesas.Where(m => m.Capacidad >= request.Personas).ToList();
        if (mesasConCapacidad.Count == 0)
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        var franjas = await _franjaHorariaRepository.ListarActivasAsync(cancellationToken);
        var ocupadas = await _reservaRepository.ListarVigentesPorFechaAsync(request.Fecha, cancellationToken);

        var ahoraLocal = _timeProvider.AhoraLocal();
        var disponibles = new List<FranjaDisponibleDto>();

        foreach (var franja in franjas.OrderBy(f => f.HoraInicio))
        {
            var inicioFranjaLocal = request.Fecha.ToDateTime(franja.HoraInicio);
            if (inicioFranjaLocal < ahoraLocal + _parametros.AntelacionMinima)
            {
                continue;
            }

            var hayMesaLibre = mesasConCapacidad.Any(mesa =>
                !ocupadas.Any(reserva => reserva.MesaId == mesa.Id && reserva.FranjaHorariaId == franja.Id));

            if (hayMesaLibre)
            {
                disponibles.Add(new FranjaDisponibleDto(franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin));
            }
        }

        return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(disponibles);
    }
}
