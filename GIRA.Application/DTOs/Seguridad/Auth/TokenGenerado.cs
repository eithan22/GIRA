namespace GIRA.Application.DTOs.Seguridad;

/// <summary>Token recién emitido y su fecha de vencimiento en UTC.</summary>
public record TokenGenerado(string Token, DateTime ExpiraEn);
