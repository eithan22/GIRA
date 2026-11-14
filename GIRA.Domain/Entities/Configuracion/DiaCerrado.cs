using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Configuracion;

/// <summary>Fecha concreta en la que el restaurante permanece cerrado (feriado, evento, etc.).</summary>
public class DiaCerrado : BaseEntity
{
    /// <summary>Fecha cerrada.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Motivo opcional, solo informativo.</summary>
    public string? Motivo { get; set; }
}
