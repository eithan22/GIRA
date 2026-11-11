namespace GIRA.Application.DTOs.Reservas;

/// <summary>Búsqueda de clientes por nombre o teléfono (GI-164).</summary>
public class BuscarClientesRequest
{
    /// <summary>Texto a buscar: parte del nombre o de los dígitos del teléfono. Mínimo 3 caracteres.</summary>
    public string? Buscar { get; init; }

    /// <summary>Número de página, desde 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página, entre 1 y 100.</summary>
    public int PageSize { get; init; } = 20;
}
