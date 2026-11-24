namespace GIRA.Application.DTOs.Personal;

/// <summary>Cargo tal como se entrega al cliente.</summary>
/// <param name="Id">Identificador del cargo.</param>
/// <param name="Nombre">Nombre del cargo.</param>
/// <param name="Descripcion">Descripción opcional.</param>
public sealed record CargoDto(Guid Id, string Nombre, string? Descripcion);

/// <summary>Datos para crear o editar un cargo.</summary>
/// <param name="Nombre">Nombre del cargo; obligatorio y único.</param>
/// <param name="Descripcion">Descripción opcional.</param>
public sealed record CargoRequest(string Nombre, string? Descripcion);
