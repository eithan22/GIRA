using GIRA.Domain.Entities.Configuracion;

namespace GIRA.Application.Interfaces.Repository.Configuracion;

/// <summary>Configuración de reservas tal como está guardada (sin interpretar).</summary>
/// <param name="Configuracion">Fila única de políticas; null si todavía no se sembró ni se guardó.</param>
/// <param name="Horarios">Horarios guardados (puede faltar algún día).</param>
/// <param name="DiasCerrados">Fechas cerradas guardadas.</param>
public sealed record ConfiguracionGuardada(
    ConfiguracionRestaurante? Configuracion,
    IReadOnlyList<HorarioOperacion> Horarios,
    IReadOnlyList<DiaCerrado> DiasCerrados);

/// <summary>Acceso a la configuración de reservas (esquema <c>configuracion</c>).</summary>
public interface IConfiguracionRepository
{
    /// <summary>Lee la configuración completa, sin seguimiento de cambios.</summary>
    Task<ConfiguracionGuardada> LeerAsync(CancellationToken cancellationToken);
}
