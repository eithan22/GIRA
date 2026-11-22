using GIRA.Application.Common;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.Services.Analitica;

public sealed class RecomendacionesService(
    IHistorialAfluenciaService historial, IPrediccionAfluenciaService predicciones,
    IRecomendacionesRepository repositorio, OpcionesRecomendaciones opciones,
    TimeProvider reloj, IPublisher publicador, ILogger<RecomendacionesService> logger) : IRecomendacionesService
{
    public async Task ActualizarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal();
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var desdeUtc = TimeZoneInfo.ConvertTimeToUtc(inicioMes.ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria);
        var hastaUtc = TimeZoneInfo.ConvertTimeToUtc(hoy.AddDays(1).ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria);
        var registros = await historial.ConsultarAsync(hoy.AddDays(-27), hoy, ct);
        var pronosticos = await predicciones.ConsultarAsync(hoy, hoy.AddDays(14), null, ct);
        var alertas = await repositorio.ContarAlertasAsync(desdeUtc, hastaUtc, ct);
        var candidatas = ReglasRecomendaciones.Calcular(hoy, registros, pronosticos, alertas, opciones);
        var nuevas = await repositorio.GuardarVigentesAsync(candidatas, reloj.GetUtcNow(), ct);
        foreach (var recomendacion in nuevas)
        {
            try
            {
                await publicador.Publish(new RecomendacionGeneradaEvent(
                    recomendacion.Id, recomendacion.Fecha, recomendacion.Tipo, recomendacion.Mensaje,
                    recomendacion.Prioridad, recomendacion.Origen, recomendacion.FechaGeneracion), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo publicar el evento de la recomendación {RecomendacionId}.",
                    recomendacion.Id);
            }
        }
    }

    public Task<IReadOnlyList<RecomendacionDto>> ConsultarAsync(string? estado, CancellationToken ct) =>
        repositorio.ConsultarAsync(estado, ct);

    public Task<bool> CambiarEstadoAsync(Guid id, string estado, CancellationToken ct) =>
        repositorio.CambiarEstadoAsync(id, estado, ct);
}
