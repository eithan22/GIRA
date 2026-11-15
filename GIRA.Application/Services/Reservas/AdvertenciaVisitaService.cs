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

        var vigentes = await _reservas.ListarVigentesDeMesaAsync(mesaId, fecha, cancellationToken);
        if (vigentes.Count == 0)
        {
            return OperationResult.Success(SinAviso);
        }

        // Una por una: a lo sumo una por franja (índice único filtrado), y una mesa no tiene más franjas
        // que las que existen, así que no hace falta cargar todas las franjas por adelantado.
        (Reserva Reserva, FranjaHoraria Franja)? masCercana = null;
        foreach (var reserva in vigentes)
        {
            var franja = await _franjas.BuscarPorIdAsync(reserva.FranjaHorariaId, cancellationToken);
            if (franja is null) continue; // violación de integridad, no debería pasar; se ignora, no se rompe el aviso

            var esPosterior = franja.HoraInicio > franjaReferencia.HoraInicio;
            var noHaTerminado = !esHoy || fecha.ToDateTime(franja.HoraFin) > ahoraLocal;
            if (!esPosterior || !noHaTerminado) continue;

            if (masCercana is null || franja.HoraInicio < masCercana.Value.Franja.HoraInicio)
            {
                masCercana = (reserva, franja);
            }
        }

        if (masCercana is null)
        {
            return OperationResult.Success(SinAviso);
        }

        var (reservaMasCercana, franjaMasCercana) = masCercana.Value;
        int? minutosFaltantes = esHoy
            ? Math.Max(0, (int)Math.Round((fecha.ToDateTime(franjaMasCercana.HoraInicio) - ahoraLocal).TotalMinutes))
            : null;

        var mensaje = MensajesReservas.AvisoProximaReserva(
            mesa.Numero, franjaMasCercana.HoraInicio, franjaMasCercana.Nombre, reservaMasCercana.Personas, minutosFaltantes);

        return OperationResult.Success(new AdvertenciaMesaDto(
            true, mensaje, franjaMasCercana.Nombre, franjaMasCercana.HoraInicio, reservaMasCercana.Personas, minutosFaltantes));
    }
}
