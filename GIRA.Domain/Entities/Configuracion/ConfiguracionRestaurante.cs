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

    /// <summary>Máximo de reservas vigentes que puede tener un mismo teléfono.</summary>
    public int MaxReservasVigentesPorTelefono { get; set; }

    /// <summary>Máximo de personas por reserva (el efectivo también depende de la mesa activa más grande).</summary>
    public int MaxPersonasPorReserva { get; set; }
}
