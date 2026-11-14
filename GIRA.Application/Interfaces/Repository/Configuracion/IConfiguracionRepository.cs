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

    /// <summary>Fila única de políticas, con seguimiento de cambios; null si todavía no existe.</summary>
    Task<ConfiguracionRestaurante?> BuscarConfiguracionAsync(CancellationToken cancellationToken);

    /// <summary>Agrega la fila única de políticas (la primera vez que se guarda).</summary>
    void AgregarConfiguracion(ConfiguracionRestaurante configuracion);

    /// <summary>Horario de un día, con seguimiento de cambios; null si ese día no tiene fila.</summary>
    Task<HorarioOperacion?> BuscarHorarioAsync(DayOfWeek dia, CancellationToken cancellationToken);

    /// <summary>Agrega el horario de un día que todavía no tiene fila.</summary>
    void AgregarHorario(HorarioOperacion horario);

    /// <summary>Días cerrados desde <paramref name="desde"/> (inclusive), por fecha, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<DiaCerrado>> ListarDiasCerradosDesdeAsync(DateOnly desde, CancellationToken cancellationToken);

    /// <summary>True si la fecha ya está cerrada.</summary>
    Task<bool> ExisteDiaCerradoAsync(DateOnly fecha, CancellationToken cancellationToken);

    /// <summary>Día cerrado por id, con seguimiento de cambios; null si no existe.</summary>
    Task<DiaCerrado?> BuscarDiaCerradoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Agrega un día cerrado.</summary>
    void AgregarDiaCerrado(DiaCerrado diaCerrado);

    /// <summary>Marca un día cerrado como eliminado (borrado lógico).</summary>
    void EliminarDiaCerrado(DiaCerrado diaCerrado);

    /// <summary>
    /// Guarda los cambios. Lanza <c>ConfiguracionConflictoConcurrenciaException</c> si otro guardó antes
    /// (RowVersion) y <c>ConfiguracionDuplicadaException</c> si choca con un registro que otro acaba de crear.
    /// </summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
