namespace GIRA.Application.DTOs.Reservas;

/// <summary>Datos para consultar la disponibilidad de una fecha.</summary>
public record ConsultarDisponibilidadRequest(DateOnly Fecha, int Personas);
