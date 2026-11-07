using GIRA.Domain.Enums.Reservas;
using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una reserva nueva queda Confirmada (web, teléfono o presencial).
/// </summary>
public sealed record ReservaConfirmadaEvent(
    Guid ReservaId,
    string Codigo,
    Guid ClienteId,
    Guid MesaId,
    Guid FranjaHorariaId,
    DateOnly Fecha,
    int Personas,
    OrigenReserva Origen) : INotification;
