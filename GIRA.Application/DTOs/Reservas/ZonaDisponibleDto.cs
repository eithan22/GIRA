namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Una zona que, en una franja y fecha dadas, tiene al menos una mesa activa libre con capacidad suficiente
/// (GI-4xx: zona opcional en /reservar). Sin mesas ni números: eso lo decide el servidor al confirmar.
/// </summary>
public record ZonaDisponibleDto(Guid Id, string Nombre);
