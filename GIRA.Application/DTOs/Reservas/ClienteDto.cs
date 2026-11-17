namespace GIRA.Application.DTOs.Reservas;

/// <summary>Cliente devuelto por la búsqueda del personal (GI-164).</summary>
public record ClienteDto(Guid Id, string Nombre, string Telefono);
