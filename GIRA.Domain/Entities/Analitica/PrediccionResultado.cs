using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Analitica;

/// <summary>Representa una predicción de afluencia para una fecha y hora.</summary>
public class PrediccionResultado : BaseEntity
{
    /// <summary>Fecha pronosticada.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Hora de inicio de la franja pronosticada.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Cantidad estimada de comensales.</summary>
    public int AfluenciaEstimada { get; set; }

    /// <summary>Fecha y hora UTC en que se generó la predicción.</summary>
    public DateTime FechaGeneracion { get; set; }

    /// <summary>Nombre del algoritmo que generó el resultado.</summary>
    public string Modelo { get; set; } = string.Empty;
}
