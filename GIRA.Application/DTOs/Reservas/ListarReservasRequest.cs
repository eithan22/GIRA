namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Filtros y paginación de la agenda del personal (GI-164). Sin <see cref="Fecha"/> ni
/// <see cref="Telefono"/> se usa el día de hoy (hora de Santo Domingo).
/// </summary>
public class ListarReservasRequest
{
    /// <summary>Día de la agenda. Si es null y tampoco hay teléfono, se usa hoy.</summary>
    public DateOnly? Fecha { get; init; }

    /// <summary>Nombre del estado (Confirmada, EnCurso, Completada, Cancelada, NoAsistio), sin distinguir mayúsculas.</summary>
    public string? Estado { get; init; }

    /// <summary>Franja horaria a filtrar.</summary>
    public Guid? FranjaId { get; init; }

    /// <summary>Teléfono exacto del cliente (10 dígitos); admite espacios y guiones.</summary>
    public string? Telefono { get; init; }

    /// <summary>Número de página, desde 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página, entre 1 y 100.</summary>
    public int PageSize { get; init; } = 20;
}
