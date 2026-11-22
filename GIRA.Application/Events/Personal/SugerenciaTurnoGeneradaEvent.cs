using MediatR;

namespace GIRA.Application.Events.Personal;

/// <summary>
/// Se publica cuando se genera una sugerencia de personal (GI-349, SRS 3.4). Notificaciones puede avisar al gerente.
/// Lleva solo identificadores, la fecha y la diferencia, sin datos personales.
/// </summary>
public sealed record SugerenciaTurnoGeneradaEvent(
    Guid SugerenciaId,
    DateOnly Fecha,
    Guid FranjaHorariaId,
    Guid CargoId,
    int Diferencia) : INotification;
