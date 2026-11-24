namespace GIRA.Application.DTOs.Personal;

/// <summary>Requerimiento de personal de un cargo tal como se entrega al cliente.</summary>
public sealed record RequerimientoPersonalDto(Guid Id, Guid CargoId, int ComensalesPorEmpleado, int MinimoPorFranja);

/// <summary>Crea o actualiza el requerimiento de un cargo (solo hay uno por cargo).</summary>
public sealed record RequerimientoPersonalRequest(Guid CargoId, int ComensalesPorEmpleado, int MinimoPorFranja);
