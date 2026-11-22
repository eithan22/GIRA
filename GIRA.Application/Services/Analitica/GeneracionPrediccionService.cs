using GIRA.Application.Common;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Analitica;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.Services.Analitica;

public sealed class GeneracionPrediccionService(
    IGeneracionPrediccionRepository repositorio,
    IHistorialAfluenciaService historial,
    IModelosActivosRepository modelosActivos,
    IModeloSsaService ssa,
    IParametrosReservaService parametros,
    IPublisher publicador,
    TimeProvider reloj,
    ILogger<GeneracionPrediccionService> logger) : IGeneracionPrediccionService
{
    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        await PublicarPendientesAsync(cancellationToken);

        var ahora = reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahora);
        var configuracion = await parametros.ObtenerAsync(cancellationToken);
        if (!configuracion.Horarios.TryGetValue(hoy.DayOfWeek, out var horario))
            throw new InvalidOperationException("No hay horario configurado para programar la predicción.");
        if (TimeOnly.FromDateTime(ahora) < horario.HoraCierre ||
            await repositorio.ExisteAsync(hoy, cancellationToken))
            return;

        var franjas = await repositorio.ObtenerFranjasActivasAsync(cancellationToken);
        var datos = await historial.ConsultarAsync(hoy.AddDays(-365), hoy, cancellationToken);
        var id = Guid.NewGuid();
        var generadaEn = reloj.GetUtcNow();
        var predicciones = PrediccionBase.Generar(hoy, generadaEn, id, franjas, datos)
            .ToDictionary(p => (p.Fecha, p.FranjaHorariaId));
        var modelos = await modelosActivos.ObtenerModelosActivosAsync(cancellationToken);
        foreach (var modelo in modelos)
        {
            var nuevosDatos = ObtenerObservacionesPosteriores(modelo, datos, hoy);
            if (nuevosDatos is null)
                continue;

            PronosticoSsa pronostico;
            try
            {
                pronostico = ssa.Predecir(modelo.ArchivoModelo, nuevosDatos, cancellationToken);
                if (pronostico.Pronostico.Count != ModeloSsaParametros.HorizonteDias ||
                    pronostico.LimiteInferior.Count != ModeloSsaParametros.HorizonteDias ||
                    pronostico.LimiteSuperior.Count != ModeloSsaParametros.HorizonteDias ||
                    pronostico.Pronostico.Concat(pronostico.LimiteInferior).Concat(pronostico.LimiteSuperior)
                        .Any(valor => !float.IsFinite(valor)))
                {
                    throw new InvalidOperationException($"El modelo {modelo.Id} no devolvió un horizonte SSA válido.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "No se pudo utilizar el modelo SSA {ModeloId} para la generación {GeneracionId}; se conservará el modelo base de baja confianza.",
                    modelo.Id, id);
                continue;
            }

            for (var i = 0; i < ModeloSsaParametros.HorizonteDias; i++)
            {
                var fecha = hoy.AddDays(i + 1);
                var limiteInferior = Math.Clamp((double)pronostico.LimiteInferior[i], 0d, int.MaxValue);
                var limiteSuperior = Math.Clamp((double)pronostico.LimiteSuperior[i], limiteInferior, int.MaxValue);
                var estimada = (int)Math.Clamp(
                    Math.Round((double)pronostico.Pronostico[i], MidpointRounding.AwayFromZero), 0d, int.MaxValue);
                predicciones[(fecha, modelo.FranjaHorariaId)] = new PrediccionAfluencia
                {
                    Id = Guid.NewGuid(),
                    GeneracionId = id,
                    Fecha = fecha,
                    FranjaHorariaId = modelo.FranjaHorariaId,
                    AfluenciaEstimada = estimada,
                    LimiteInferior = (decimal)limiteInferior,
                    LimiteSuperior = (decimal)limiteSuperior,
                    BajaConfianza = false,
                    GeneradaEn = generadaEn,
                    VersionModelo = modelo.Version,
                    Vigente = true
                };
            }
        }

        var resultados = predicciones.Values
            .Where(p => configuracion.EstaAbierto(p.Fecha)).ToList();
        if (resultados.Count == 0)
        {
            logger.LogWarning(
                "No se generaron predicciones para {FechaProceso}: faltan observaciones útiles o fechas operativas en el horizonte.",
                hoy);
            return;
        }

        var publicaciones = resultados.GroupBy(p => p.Fecha)
            .Where(g => franjas.Count > 0 && g.Select(p => p.FranjaHorariaId).Distinct().Count() == franjas.Count)
            .Select(g => new PublicacionPrediccion { Id = Guid.NewGuid(), GeneracionId = id, Fecha = g.Key })
            .ToList();
        var generacion = new GeneracionPrediccion
        {
            Id = id, FechaProceso = hoy, GeneradaEn = generadaEn, CantidadPredicciones = resultados.Count
        };
        await repositorio.GuardarAsync(generacion, resultados, publicaciones, cancellationToken);
        logger.LogInformation("Generación de predicciones {GeneracionId}: {Cantidad} resultados, {Publicaciones} días completos.",
            id, resultados.Count, publicaciones.Count);
        await PublicarPendientesAsync(cancellationToken);
    }

    private static IReadOnlyList<float>? ObtenerObservacionesPosteriores(
        ModeloAfluencia modelo, IReadOnlyList<HistorialAfluenciaDto> historial, DateOnly hoy)
    {
        var desde = modelo.EntrenamientoHasta.AddDays(1);
        var dias = hoy.DayNumber - desde.DayNumber + 1;
        if (dias < 0 || dias > 365)
            return null;

        var registros = historial
            .Where(r => r.FranjaHorariaId == modelo.FranjaHorariaId && r.Fecha >= desde && r.Fecha <= hoy)
            .ToDictionary(r => r.Fecha, r => r.TotalComensales);
        var observaciones = new List<float>(dias);
        for (var i = 0; i < dias; i++)
        {
            if (!registros.TryGetValue(desde.AddDays(i), out var personas) || personas <= 0)
                return null;
            observaciones.Add(personas);
        }

        return observaciones;
    }

    private async Task PublicarPendientesAsync(CancellationToken cancellationToken)
    {
        foreach (var pendiente in await repositorio.ObtenerPendientesAsync(cancellationToken))
        {
            await publicador.Publish(pendiente.Evento, cancellationToken);
            await repositorio.MarcarPublicadaAsync(pendiente.Id, reloj.GetUtcNow(), cancellationToken);
        }
    }
}
