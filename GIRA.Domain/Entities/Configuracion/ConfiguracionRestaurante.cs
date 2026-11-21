using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Configuracion;

/// <summary>
/// Políticas de reserva del restaurante. La tabla tiene una sola fila, con el Id fijo
/// <see cref="IdUnico"/>. Quién y cuándo la modificó por última vez queda en <c>UpdatedBy</c> y
/// <c>UpdatedAt</c>, que vienen de <see cref="BaseEntity"/>.
/// </summary>
public class ConfiguracionRestaurante : BaseEntity
{
    /// <summary>Id de la única fila de configuración.</summary>
    public static readonly Guid IdUnico = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    /// <summary>Antelación mínima, en minutos, para crear una reserva (solo reservas nuevas).</summary>
    public int AntelacionMinimaMinutos { get; set; }

    /// <summary>Antelación máxima, en días, para crear una reserva (solo reservas nuevas).</summary>
    public int AntelacionMaximaDias { get; set; }

    /// <summary>Minutos antes del inicio de la franja hasta los que el cliente puede cancelar.</summary>
    public int LimiteCancelacionMinutos { get; set; }

    /// <summary>Minutos antes del inicio de la franja desde los que el personal puede hacer check-in.</summary>
    public int VentanaCheckInMinutos { get; set; }

    /// <summary>
    /// Minutos de tolerancia tras los cuales una reserva Confirmada sin check-in pasa a NoAsistio (GI-404).
    /// El plazo cuenta desde el mayor entre el inicio de la franja y el último registro de la reserva.
    /// </summary>
    public int ToleranciaNoShowMinutos { get; set; }

    /// <summary>Máximo de reservas vigentes que puede tener un mismo teléfono.</summary>
    public int MaxReservasVigentesPorTelefono { get; set; }

    /// <summary>Máximo de personas por reserva (el efectivo también depende de la mesa activa más grande).</summary>
    public int MaxPersonasPorReserva { get; set; }

    /// <summary>
    /// Teléfono de contacto del restaurante que se muestra al cliente; null si no se configuró (no hay
    /// número por defecto, el restaurante lo escribe desde Configuración).
    /// </summary>
    public string? TelefonoContacto { get; set; }

    /// <summary>Valor por defecto de <see cref="DiasProyeccionInventario"/>.</summary>
    public const int DiasProyeccionInventarioPorDefecto = 3;

    /// <summary>Valor por defecto de <see cref="DiasAvisoVencimientoInventario"/>.</summary>
    public const int DiasAvisoVencimientoInventarioPorDefecto = 2;

    /// <summary>Días de consumo proyectado que Inventario resta al stock para avisar un quiebre (RN-INV-07).</summary>
    public int DiasProyeccionInventario { get; set; } = DiasProyeccionInventarioPorDefecto;

    /// <summary>Días antes del vencimiento de un lote en que Inventario avisa (RN-INV-11).</summary>
    public int DiasAvisoVencimientoInventario { get; set; } = DiasAvisoVencimientoInventarioPorDefecto;
}
