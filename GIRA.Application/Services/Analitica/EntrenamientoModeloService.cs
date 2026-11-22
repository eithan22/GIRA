using GIRA.Application.Common;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Entities.Analitica;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.Services.Analitica;

public sealed class EntrenamientoModeloService(
    IModelosAfluenciaRepository modelos,
    IHistorialAfluenciaService historial,
    IModeloSsaService ssa,
    IPublisher publicador,
    TimeProvider reloj,
    ILogger<EntrenamientoModeloService> logger) : IEntrenamientoModeloService
{
    private const int DiasTotalesMinimos =
        ModeloSsaParametros.DiasEntrenamientoMinimos + ModeloSsaParametros.DiasValidacion;

    public Task<Guid> SolicitarAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        modelos.SolicitarEntrenamientoAsync(usuarioId, reloj.GetUtcNow(), cancellationToken);

    public async Task<EntrenamientoModeloDto?> ConsultarAsync(Guid id, CancellationToken cancellationToken)
    {
        var trabajo = await modelos.ObtenerEntrenamientoAsync(id, cancellationToken);
        if (trabajo is null)
            return null;

        var nombres = await modelos.ObtenerNombresFranjasAsync(
            trabajo.Resultados.Select(r => r.FranjaHorariaId).Distinct().ToList(), cancellationToken);
        return new EntrenamientoModeloDto(
            trabajo.Id,
            trabajo.Estado.ToString(),
            trabajo.SolicitadoEn,
            trabajo.IniciadoEn,
            trabajo.FinalizadoEn,
            trabajo.Error,
            trabajo.Resultados
                .OrderBy(r => r.FranjaHorariaId)
                .Select(r => new ResultadoEntrenamientoModeloDto(
                    r.FranjaHorariaId,
                    nombres.GetValueOrDefault(r.FranjaHorariaId, r.FranjaHorariaId.ToString()),
                    r.Estado.ToString(),
                    r.ObservacionesEntrenamiento,
                    r.ObservacionesValidacion,
                    r.Mae,
                    r.Rmse,
                    r.MaeReferencia,
                    r.VersionModelo))
                .ToList());
    }

    public Task<IReadOnlyList<ModeloAfluenciaDto>> ListarModelosAsync(CancellationToken cancellationToken) =>
        modelos.ObtenerModelosAsync(cancellationToken);

    public async Task<ResultadoActivacionModelo> ActivarModeloAsync(
        Guid modeloId, Guid usuarioId, string motivo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 500)
            throw new ArgumentException("El motivo es obligatorio y no puede superar 500 caracteres.", nameof(motivo));

        return await modelos.ActivarModeloAsync(modeloId, usuarioId, motivo.Trim(),
            reloj.GetUtcNow(), esRollback: true, cancellationToken);
    }

    public async Task<bool> ProcesarSiguienteAsync(CancellationToken cancellationToken)
    {
        var trabajo = await modelos.IniciarSiguienteEntrenamientoAsync(reloj.GetUtcNow(), cancellationToken);
        if (trabajo is null)
            return false;

        try
        {
            var fin = reloj.HoyLocal().AddDays(-1);
            var inicio = fin.AddDays(-(DiasTotalesMinimos - 1));
            var observaciones = await historial.ConsultarAsync(inicio, fin, cancellationToken);
            var franjas = await modelos.ObtenerFranjasActivasAsync(cancellationToken);
            var modelosActivos = (await modelos.ObtenerModelosActivosAsync(cancellationToken))
                .ToDictionary(m => m.FranjaHorariaId);
            var nuevosModelos = new List<ModeloAfluencia>();
            var resultados = new List<ResultadoEntrenamientoModelo>();

            foreach (var franjaId in franjas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var serie = Enumerable.Range(0, DiasTotalesMinimos)
                    .Select(desplazamiento => inicio.AddDays(desplazamiento))
                    .Select(fecha => (Fecha: fecha, Valor: observaciones
                        .Where(r => r.Fecha == fecha && r.FranjaHorariaId == franjaId)
                        .Select(r => (float)r.TotalComensales)
                        .SingleOrDefault()))
                    .ToList();
                var completas = serie.All(d => observaciones.Any(r =>
                    r.Fecha == d.Fecha && r.FranjaHorariaId == franjaId && r.TotalComensales > 0));

                if (!completas)
                {
                    resultados.Add(new ResultadoEntrenamientoModelo
                    {
                        Id = Guid.NewGuid(),
                        EntrenamientoId = trabajo.Id,
                        FranjaHorariaId = franjaId,
                        Estado = EstadoResultadoEntrenamiento.DatosInsuficientes,
                        ObservacionesEntrenamiento = serie.Take(ModeloSsaParametros.DiasEntrenamientoMinimos)
                            .Count(d => d.Valor > 0),
                        ObservacionesValidacion = serie.Skip(ModeloSsaParametros.DiasEntrenamientoMinimos)
                            .Count(d => d.Valor > 0)
                    });
                    continue;
                }

                var entrenamiento = serie.Take(ModeloSsaParametros.DiasEntrenamientoMinimos).ToList();
                var validacion = serie.Skip(ModeloSsaParametros.DiasEntrenamientoMinimos).ToList();
                var entrenamientoSsa = ssa.Entrenar(entrenamiento.Select(d => d.Valor).ToList(), cancellationToken);
                var mae = CalcularMae(entrenamientoSsa.Pronostico, validacion);
                var rmse = CalcularRmse(entrenamientoSsa.Pronostico, validacion);
                var maeBase = CalcularMaeBase(entrenamiento, validacion);
                var modeloVigente = modelosActivos.GetValueOrDefault(franjaId);
                var maeReferencia = modeloVigente?.Mae ?? maeBase;
                var activar = mae <= maeReferencia;
                var modeloId = Guid.NewGuid();
                var version = $"ssa-v1-{modeloId:N}";

                nuevosModelos.Add(new ModeloAfluencia
                {
                    Id = modeloId,
                    EntrenamientoId = trabajo.Id,
                    FranjaHorariaId = franjaId,
                    Version = version,
                    EntrenamientoDesde = entrenamiento[0].Fecha,
                    EntrenamientoHasta = entrenamiento[^1].Fecha,
                    ValidacionDesde = validacion[0].Fecha,
                    ValidacionHasta = validacion[^1].Fecha,
                    Mae = mae,
                    Rmse = rmse,
                    ArchivoModelo = entrenamientoSsa.ArchivoModelo,
                    CreadoEn = reloj.GetUtcNow(),
                    Activo = activar
                });
                resultados.Add(new ResultadoEntrenamientoModelo
                {
                    Id = Guid.NewGuid(),
                    EntrenamientoId = trabajo.Id,
                    FranjaHorariaId = franjaId,
                    ModeloId = modeloId,
                    Estado = activar
                        ? EstadoResultadoEntrenamiento.Activado
                        : EstadoResultadoEntrenamiento.NoMejoraModeloVigente,
                    ObservacionesEntrenamiento = entrenamiento.Count,
                    ObservacionesValidacion = validacion.Count,
                    Mae = mae,
                    Rmse = rmse,
                    MaeReferencia = maeReferencia,
                    VersionModelo = version
                });
            }

            await modelos.GuardarResultadosAsync(trabajo.Id, resultados, nuevosModelos,
                reloj.GetUtcNow(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await modelos.ReencolarEntrenamientoAsync(trabajo.Id, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            const string error = "El entrenamiento falló. Revisa los registros del servidor.";
            await modelos.MarcarEntrenamientoFallidoAsync(trabajo.Id, error, reloj.GetUtcNow(), cancellationToken);
            logger.LogError(ex, "Falló el entrenamiento de Analítica {EntrenamientoId}.", trabajo.Id);
            try
            {
                await publicador.Publish(new EntrenamientoModeloFallidoEvent(trabajo.Id, error),
                    CancellationToken.None);
            }
            catch (Exception errorPublicacion)
            {
                logger.LogError(errorPublicacion,
                    "No se pudo publicar el evento de fallo del entrenamiento {EntrenamientoId}.", trabajo.Id);
            }
        }

        return true;
    }

    private static decimal CalcularMae(IReadOnlyList<float> pronostico,
        IReadOnlyList<(DateOnly Fecha, float Valor)> validacion)
    {
        if (pronostico.Count != validacion.Count)
            throw new InvalidOperationException("El modelo SSA no devolvió el horizonte de validación completo.");

        return (decimal)pronostico.Zip(validacion, (estimado, real) => Math.Abs(estimado - real.Valor)).Average();
    }

    private static decimal CalcularRmse(IReadOnlyList<float> pronostico,
        IReadOnlyList<(DateOnly Fecha, float Valor)> validacion)
    {
        if (pronostico.Count != validacion.Count)
            throw new InvalidOperationException("El modelo SSA no devolvió el horizonte de validación completo.");

        return (decimal)Math.Sqrt(pronostico.Zip(validacion,
            (estimado, real) => Math.Pow(estimado - real.Valor, 2)).Average());
    }

    private static decimal CalcularMaeBase(
        IReadOnlyList<(DateOnly Fecha, float Valor)> entrenamiento,
        IReadOnlyList<(DateOnly Fecha, float Valor)> validacion)
    {
        var referencia = entrenamiento.TakeLast(28)
            .GroupBy(d => d.Fecha.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Average(d => d.Valor));
        return (decimal)validacion.Average(d => Math.Abs(referencia[d.Fecha.DayOfWeek] - d.Valor));
    }
}
