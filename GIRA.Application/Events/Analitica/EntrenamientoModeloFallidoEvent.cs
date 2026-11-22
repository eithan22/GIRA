using MediatR;

namespace GIRA.Application.Events.Analitica;

public sealed record EntrenamientoModeloFallidoEvent(Guid EntrenamientoId, string Error) : INotification;
