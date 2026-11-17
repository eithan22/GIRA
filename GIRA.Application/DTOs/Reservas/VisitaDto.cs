namespace GIRA.Application.DTOs.Reservas;

/// <summary>Visita sin reserva registrada (GI-166).</summary>
public record VisitaDto(
    Guid Id,
    DateOnly Fecha,
    Guid FranjaHorariaId,
    string FranjaNombre,
    Guid? MesaId,
    int? MesaNumero,
    int Personas);
