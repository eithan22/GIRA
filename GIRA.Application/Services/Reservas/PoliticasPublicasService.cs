using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IPoliticasPublicasService"/>.</summary>
public class PoliticasPublicasService : IPoliticasPublicasService
{
    private readonly IParametrosReservaService _parametros;
    private readonly IMesaRepository _mesas;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con los parámetros, las mesas y el reloj.</summary>
    public PoliticasPublicasService(IParametrosReservaService parametros, IMesaRepository mesas, TimeProvider reloj)
    {
        _parametros = parametros;
        _mesas = mesas;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<PoliticasPublicasDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var parametros = await _parametros.ObtenerAsync(cancellationToken);
        var mesas = await _mesas.ListarActivasAsync(cancellationToken);

        // Tamaño efectivo de grupo: el configurado, sin pasar de la mesa activa más grande.
        var capacidadMaxima = mesas.Count == 0 ? 0 : mesas.Max(m => m.Capacidad);
        var maxPersonas = Math.Min(parametros.MaxPersonasPorReserva, capacidadMaxima);

        // Solo las fechas cerradas que el cliente puede llegar a elegir: dentro de la ventana de reserva.
        var hoy = _reloj.HoyLocal();
        var hasta = hoy.AddDays(parametros.AntelacionMaxima.Days);
        var diasCerrados = parametros.DiasCerrados
            .Where(fecha => fecha >= hoy && fecha <= hasta)
            .OrderBy(fecha => fecha)
            .ToList();

        return new PoliticasPublicasDto(
            (int)parametros.AntelacionMinima.TotalMinutes,
            parametros.AntelacionMaxima.Days,
            (int)parametros.LimiteCancelacion.TotalMinutes,
            maxPersonas,
            diasCerrados);
    }
}
