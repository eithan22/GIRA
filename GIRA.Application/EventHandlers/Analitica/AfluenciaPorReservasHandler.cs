using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.EventHandlers.Analitica;

/// <summary>Registra visitas completadas y reservas no asistidas sin modificar Reservas.</summary>
public sealed class AfluenciaPorReservasHandler(
    IServiceScopeFactory scopes,
    ILogger<AfluenciaPorReservasHandler> logger) :
    INotificationHandler<VisitaCompletadaEvent>,
    INotificationHandler<ReservaNoAsistidaEvent>
{
    public Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken) =>
        RegistrarAsync(new RegistroAfluencia
        {
            ReferenciaId = notification.VisitaId,
            Origen = notification.ConReserva ? OrigenRegistroAfluencia.Reserva : OrigenRegistroAfluencia.VisitaSinReserva,
            Tipo = TipoRegistroAfluencia.VisitaCompletada,
            Fecha = notification.Fecha,
            FranjaHorariaId = notification.FranjaHorariaId,
            Personas = notification.PersonasReales
        }, cancellationToken);

    public Task Handle(ReservaNoAsistidaEvent notification, CancellationToken cancellationToken) =>
        RegistrarAsync(new RegistroAfluencia
        {
            ReferenciaId = notification.ReservaId,
            Origen = OrigenRegistroAfluencia.Reserva,
            Tipo = TipoRegistroAfluencia.ReservaNoAsistida,
            Fecha = notification.Fecha,
            FranjaHorariaId = notification.FranjaHorariaId,
            Personas = notification.Personas
        }, cancellationToken);

    private async Task RegistrarAsync(RegistroAfluencia registro, CancellationToken cancellationToken)
    {
        try
        {
            if (registro.ReferenciaId == Guid.Empty || registro.FranjaHorariaId == Guid.Empty ||
                registro.Fecha == default || registro.Personas <= 0)
            {
                throw new ArgumentException("El evento de afluencia contiene datos inválidos.");
            }

            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRegistroAfluenciaRepository>()
                .RegistrarAsync(registro, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "No se pudo registrar la afluencia de {Origen} {ReferenciaId}; se requiere revisión.",
                registro.Origen, registro.ReferenciaId);
        }
    }
}
