using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Configuracion;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Implementación de <see cref="IConsultaSalon"/> (puerto de Configuración) con los repositorios de Reservas:
/// Configuración pregunta por franjas, mesas y reservas sin conocer sus entidades.
/// </summary>
public class ConsultaSalonService : IConsultaSalon
{
    private readonly IFranjaHorariaRepository _franjas;
    private readonly IMesaRepository _mesas;
    private readonly IReservaRepository _reservas;
    private readonly IReservasFuturasRepository _reservasFuturas;

    /// <summary>Crea el servicio con los repositorios de franjas, mesas y reservas.</summary>
    public ConsultaSalonService(
        IFranjaHorariaRepository franjas,
        IMesaRepository mesas,
        IReservaRepository reservas,
        IReservasFuturasRepository reservasFuturas)
    {
        _franjas = franjas;
        _mesas = mesas;
        _reservas = reservas;
        _reservasFuturas = reservasFuturas;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FranjaActivaInfo>> ListarFranjasActivasAsync(CancellationToken cancellationToken)
    {
        var franjas = await _franjas.ListarActivasAsync(cancellationToken);
        return franjas
            .OrderBy(f => f.HoraInicio)
            .Select(f => new FranjaActivaInfo(f.Id, f.Nombre, f.HoraInicio, f.HoraFin))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CapacidadMaximaMesaActivaAsync(CancellationToken cancellationToken)
    {
        var mesas = await _mesas.ListarActivasAsync(cancellationToken);
        return mesas.Count == 0 ? 0 : mesas.Max(m => m.Capacidad);
    }

    /// <inheritdoc />
    public async Task<int> ContarReservasVigentesAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        var vigentes = await _reservas.ListarVigentesPorFechaAsync(fecha, cancellationToken);
        return vigentes.Count;
    }

    /// <inheritdoc />
    public Task<int> ContarReservasVigentesPorDiaSemanaAsync(DayOfWeek dia, DateOnly desde, CancellationToken cancellationToken) =>
        _reservasFuturas.ContarPorDiaSemanaAsync(dia, desde, cancellationToken);
}
