namespace GIRA.Application.DTOs.Seguridad;

public class HistorialAccesosRequest
{
    /// <summary>Número de página, empezando en 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Cantidad de registros por página (1 a 100).</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Si se indica, devuelve solo los intentos exitosos (true) o solo los fallidos (false).</summary>
    public bool? Exitoso { get; set; }

    /// <summary>Fecha y hora mínima del intento, inclusive. Se interpreta en UTC.</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Fecha y hora máxima del intento, inclusive. Se interpreta en UTC.</summary>
    public DateTime? Hasta { get; set; }
}
