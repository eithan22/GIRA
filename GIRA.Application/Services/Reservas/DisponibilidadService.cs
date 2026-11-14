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
        ConsultarDisponibilidadRequest request, CancellationToken cancellationToken,
        AudienciaReserva audiencia = AudienciaReserva.Cliente)
    {
        var validacion = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                ErrorType.Validation, validacion.ToString());
        }

        var parametros = await _parametros.ObtenerAsync(cancellationToken);
        var mesas = await _mesaRepository.ListarActivasAsync(cancellationToken);
        if (mesas.Count == 0)
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        // Tamaño efectivo del grupo: el configurado, sin pasar de la mesa activa más grande.
        var tamanoMaximo = Math.Min(parametros.MaxPersonasPorReserva, mesas.Max(m => m.Capacidad));
        if (request.Personas > tamanoMaximo)
        {
            return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                ErrorType.Validation,
                MensajesReservas.GrupoGrande(audiencia, tamanoMaximo));
        }

        if (!parametros.EstaAbierto(request.Fecha))
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
            // Cliente: antelación mínima desde el inicio. Personal: sin antelación, solo se excluye la franja
            // que ya terminó (el fin exacto cuenta como terminada, igual que en el check-in).
            var excluida = audiencia == AudienciaReserva.Personal
                ? ahoraLocal >= request.Fecha.ToDateTime(franja.HoraFin)
                : request.Fecha.ToDateTime(franja.HoraInicio) < ahoraLocal + parametros.AntelacionMinima;
            if (excluida)
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
