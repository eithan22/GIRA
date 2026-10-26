namespace GIRA.Application.DTOs.Seguridad;

public record RegistroAccesoDto(
 Guid Id,
 Guid? UsuarioId,
 string CorreoIntentado,
 DateTime Fecha,
 string Ip,
 bool Exitoso,
 string? Motivo);
