using GIRA.Application.Common.Personal;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>
/// Implementación de <see cref="IGeneracionSugerenciasService"/> (GI-303, RF-PER-09). Requerido = mayor entre el mínimo
/// del cargo y la afluencia ÷ comensales por empleado, redondeado hacia arriba (SRS 5.4).
/// </summary>
public class GeneracionSugerenciasService : IGeneracionSugerenciasService
{
    /// <summary>Diferencia mínima, en personas, para generar una sugerencia (1 por defecto en el SRS).</summary>
    public const int UmbralDiferencia = 1;

    private readonly ISugerenciaTurnoRepository _sugerencias;
    private readonly ITurnoRepository _turnos;
    private readonly IRequerimientoPersonalRepository _requerimientos;
    private readonly IFranjaService _franjas;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con sus repositorios, las franjas de Reservas y el reloj.</summary>
    public GeneracionSugerenciasService(
        ISugerenciaTurnoRepository sugerencias,
        ITurnoRepository turnos,
        IRequerimientoPersonalRepository requerimientos,
        IFranjaService franjas,
        TimeProvider reloj)
    {
        _sugerencias = sugerencias;
        _turnos = turnos;
        _requerimientos = requerimientos;
        _franjas = franjas;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task GenerarDesdePrediccionAsync(PrediccionAfluenciaGeneradaEvent evento, CancellationToken cancellationToken)
    {
        // Primero las franjas: si no se pueden leer, no se toca ninguna sugerencia y el error sale hacia el publicador.
        var franjasResultado = await _franjas.ListarOperativasAsync(cancellationToken);
        if (franjasResultado.IsFailure)
        {
            throw new InvalidOperationException($"No se pudieron leer las franjas horarias: {franjasResultado.Error}");
        }

        // RN-PER-07: lo pendiente de las mismas fechas se reemplaza por lo que trae esta predicción.
        var pendientes = await _sugerencias.ListarPendientesEntreAsync(evento.Desde, evento.Hasta, cancellationToken);
        foreach (var anterior in pendientes)
        {
            anterior.Estado = EstadoSugerencia.Vencida;
        }

        var franjas = franjasResultado.Value!.ToDictionary(f => f.Id);
        var requerimientos = await _requerimientos.ListarAsync(cancellationToken);
        var turnos = (await _turnos.ListarEntreAsync(evento.Desde, evento.Hasta, cancellationToken)).ToList();
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        foreach (var prediccion in evento.Predicciones)
        {
            // Una franja desconocida o inactiva no tiene cobertura que medir.
            if (!franjas.TryGetValue(prediccion.FranjaHorariaId, out var franja) || !franja.Activa)
            {
                continue;
            }

            foreach (var requerimiento in requerimientos)
            {
                var porComensales = requerimiento.ComensalesPorEmpleado > 0
                    ? (int)Math.Ceiling(prediccion.AfluenciaEstimada / (double)requerimiento.ComensalesPorEmpleado)
                    : 0;
                var requerido = Math.Max(requerimiento.MinimoPorFranja, porComensales);
                var planificado = CalculoCobertura.Planificado(turnos, prediccion.Fecha, requerimiento.CargoId, franja.HoraInicio, franja.HoraFin);
                var diferencia = requerido - planificado;

                if (Math.Abs(diferencia) < UmbralDiferencia)
                {
                    continue;
                }

                _sugerencias.Agregar(new SugerenciaTurno
                {
                    Fecha = prediccion.Fecha,
                    FranjaHorariaId = prediccion.FranjaHorariaId,
                    CargoId = requerimiento.CargoId,
                    PersonalPlanificado = planificado,
                    PersonalRequerido = requerido,
                    Diferencia = diferencia,
                    // Pendiente con Isaac: el evento aún no trae el identificador de la predicción.
                    PrediccionId = null,
                    Estado = EstadoSugerencia.Pendiente,
                    FechaGeneracion = ahora,
                });
            }
        }

        await _sugerencias.GuardarCambiosAsync(cancellationToken);
    }
}
