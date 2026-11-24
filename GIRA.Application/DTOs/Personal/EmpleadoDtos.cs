namespace GIRA.Application.DTOs.Personal;

/// <summary>Empleado tal como se entrega al cliente.</summary>
public sealed record EmpleadoDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string Cedula,
    string? Telefono,
    string? Email,
    Guid CargoId,
    DateOnly FechaIngreso,
    int HorasMaximasSemana,
    bool Activo);

/// <summary>Datos para registrar o editar un empleado.</summary>
public sealed record EmpleadoRequest(
    string Nombre,
    string Apellido,
    string Cedula,
    string? Telefono,
    string? Email,
    Guid CargoId,
    DateOnly FechaIngreso,
    int HorasMaximasSemana);

/// <summary>Activa o desactiva un empleado (no se elimina).</summary>
/// <param name="Activo">true para activarlo, false para desactivarlo.</param>
public sealed record CambiarEstadoEmpleadoRequest(bool Activo);
