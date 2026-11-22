using MediatR;

namespace GIRA.Application.Events.Analitica;

public sealed record RecomendacionGeneradaEvent(
    Guid RecomendacionId,
    DateOnly Fecha,
    string Tipo,
    string Mensaje,
    string Prioridad,
    string Origen,
    DateTimeOffset GeneradaEn) : INotification;
