using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IAdvertenciaVisitaService"/>.</summary>
public class AdvertenciaVisitaService : IAdvertenciaVisitaService
{
    private readonly IReservaRepository _reservas;
    private readonly IFranjaHorariaRepository _franjas;
    private readonly IMesaGestionRepository _mesas;
    private readonly IValidator<ConsultarAdvertenciaMesaRequest> _validador;
    private readonly TimeProvider _reloj;

    private static readonly AdvertenciaMesaDto SinAviso = new(false, null, null, null, null, null);

    /// <summary>Crea el servicio con los repositorios de lectura, el validador y el reloj.</summary>
    public AdvertenciaVisitaService(
        IReservaRepository reservas,
        IFranjaHorariaRepository franjas,
        IMesaGestionRepository mesas,
        IValidator<ConsultarAdvertenciaMesaRequest> validador,
        TimeProvider reloj)
    {
        _reservas = reservas;
        _franjas = franjas;
        _mesas = mesas;
        _validador = validador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<AdvertenciaMesaDto>> ConsultarAsync(
        ConsultarAdvertenciaMesaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<AdvertenciaMesaDto>(ErrorType.Validation, validacion.ToString());
        }

        // El validador ya comprobó el formato; aquí solo se vuelve a convertir.
        ConsultarAdvertenciaMesaRequestValidator.TryParseId(request.MesaId, out var mesaId);
        ConsultarAdvertenciaMesaRequestValidator.TryParseId(request.FranjaId, out var franjaId);
        var fecha = ConsultarAdvertenciaMesaRequestValidator.TryParseFecha(request.Fecha, out var indicada)
            ? indicada
            : _reloj.HoyLocal();

        var mesa = await _mesas.BuscarPorIdAsync(mesaId, cancellationToken);
        if (mesa is null)
        {
            return OperationResult.Failure<AdvertenciaMesaDto>(ErrorType.NotFound, "No encontramos esa mesa.");
        }

        var franjaReferencia = await _franjas.BuscarPorIdAsync(franjaId, cancellationToken);
        if (franjaReferencia is null)
        {
            return OperationResult.Failure<AdvertenciaMesaDto>(ErrorType.NotFound, "No encontramos esa franja.");
        }

        var esHoy = fecha == _reloj.HoyLocal();
        var ahoraLocal = _reloj.AhoraLocal();

        // Solo importa la franja activa INMEDIATAMENTE siguiente a aquella en la que se registraría la visita
        // (la misma noción de "siguiente" que usa el registro de visitas: franjas activas ordenadas por hora de
        // inicio). Si la franja de referencia es la última activa del día, no hay próxima que avisar.
        var franjaSiguiente = (await _franjas.ListarActivasAsync(cancellationToken))
            .Where(f => f.HoraInicio > franjaReferencia.HoraInicio)
            .OrderBy(f => f.HoraInicio)
            .FirstOrDefault();
        if (franjaSiguiente is null)
        {
            return OperationResult.Success(SinAviso);
        }

        if (esHoy && fecha.ToDateTime(franjaSiguiente.HoraFin) <= ahoraLocal)
        {
            return OperationResult.Success(SinAviso);
        }

        var vigentes = await _reservas.ListarVigentesDeMesaAsync(mesaId, fecha, cancellationToken);
        var reservaSiguiente = vigentes.FirstOrDefault(r => r.FranjaHorariaId == franjaSiguiente.Id);
        if (reservaSiguiente is null)
        {
            return OperationResult.Success(SinAviso);
        }

        int? minutosFaltantes = esHoy
            ? Math.Max(0, (int)Math.Round((fecha.ToDateTime(franjaSiguiente.HoraInicio) - ahoraLocal).TotalMinutes))
            : null;

        var mensaje = MensajesReservas.AvisoProximaReserva(
            mesa.Numero, franjaSiguiente.HoraInicio, franjaSiguiente.Nombre, reservaSiguiente.Personas, minutosFaltantes);

        return OperationResult.Success(new AdvertenciaMesaDto(
            true, mensaje, franjaSiguiente.Nombre, franjaSiguiente.HoraInicio, reservaSiguiente.Personas, minutosFaltantes));
    }
}
