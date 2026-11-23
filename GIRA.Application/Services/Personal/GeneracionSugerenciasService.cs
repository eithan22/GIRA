using GIRA.Application.Events.Analitica;
using GIRA.Application.Events.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Common.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Application.DTOs.Reservas;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.Services.Personal;

/// <summary>
/// Implementación de <see cref="IGeneracionSugerenciasService"/> (GI-303, RF-PER-09). Requerido = mayor entre el mínimo
/// del cargo y la afluencia ÷ comensales por empleado, redondeado hacia arriba (SRS 5.4).
/// Valida el contrato de Analítica antes de actuar: versión 1, EventoId presente y restaurante configurado.
/// </summary>
public class GeneracionSugerenciasService : IGeneracionSugerenciasService
{
    /// <summary>Diferencia mínima, en personas, para generar una sugerencia (1 por defecto en el SRS).</summary>
    public const int UmbralDiferencia = 1;

    /// <summary>Versión del contrato de evento que este consumidor entiende (VersionContrato = 1 en Analítica).</summary>
    public const int VersionContratoSoportada = 1;

    private readonly ISugerenciaTurnoRepository _sugerencias;
    private readonly ITurnoRepository _turnos;
    private readonly IRequerimientoPersonalRepository _requerimientos;
    private readonly IFranjaService _franjas;
    private readonly TimeProvider _reloj;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly IRestauranteConfigurado _restaurante;
    private readonly ILogger<GeneracionSugerenciasService> _logger;

    /// <summary>Crea el servicio con sus repositorios, las franjas de Reservas, el reloj, el publicador, el restaurante y el registro.</summary>
    public GeneracionSugerenciasService(
        ISugerenciaTurnoRepository sugerencias,
        ITurnoRepository turnos,
        IRequerimientoPersonalRepository requerimientos,
        IFranjaService franjas,
        TimeProvider reloj,
        IPublicadorEventosSeguro publicador,
        IRestauranteConfigurado restaurante,
        ILogger<GeneracionSugerenciasService> logger)
    {
        _sugerencias = sugerencias;
        _turnos = turnos;
        _requerimientos = requerimientos;
        _franjas = franjas;
        _reloj = reloj;
        _publicador = publicador;
        _restaurante = restaurante;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerarDesdePrediccionAsync(PrediccionAfluenciaGeneradaEvent evento, CancellationToken cancellationToken)
    {
        if (!ContratoValido(evento))
        {
            // Según la documentación de Analítica, un consumidor no actúa sobre eventos de otra versión, sin identificador o de otro restaurante.
            _logger.LogWarning("Evento de predicción descartado: versión de contrato, identificador o restaurante no válidos.");
            return;
        }

        // Un evento repetido (mismo EventoId) no genera sugerencias otra vez.
        if (await _sugerencias.ExisteEventoAsync(evento.EventoId!.Value, cancellationToken))
        {
            _logger.LogInformation("Evento de predicción ya procesado; no se generan sugerencias duplicadas.");
            return;
        }

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
        var generadas = new List<SugerenciaTurno>();

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

                var nueva = new SugerenciaTurno
                {
                    Fecha = prediccion.Fecha,
                    FranjaHorariaId = prediccion.FranjaHorariaId,
                    CargoId = requerimiento.CargoId,
                    PersonalPlanificado = planificado,
                    PersonalRequerido = requerido,
                    Diferencia = diferencia,
                    EventoId = evento.EventoId,
                    PrediccionId = prediccion.PrediccionId,
                    Estado = EstadoSugerencia.Pendiente,
                    FechaGeneracion = ahora,
                };
                _sugerencias.Agregar(nueva);
                generadas.Add(nueva);
            }
        }

        await _sugerencias.GuardarCambiosAsync(cancellationToken);

        // GI-349: se publica después de guardar, nunca antes; un handler que falla no revierte nada (SRS 3.3).
        foreach (var generada in generadas)
        {
            await _publicador.PublicarAsync(
                new SugerenciaTurnoGeneradaEvent(generada.Id, generada.Fecha, generada.FranjaHorariaId, generada.CargoId, generada.Diferencia),
                $"SugerenciaId={generada.Id}",
                cancellationToken);
        }
    }

    /// <summary>
    /// El evento se acepta solo si trae identificador, tiene la versión que entendemos y es del restaurante configurado.
    /// Si el restaurante no está configurado, no se acepta ningún evento.
    /// </summary>
    private bool ContratoValido(PrediccionAfluenciaGeneradaEvent evento)
    {
        var configurado = _restaurante.RestauranteId;
        return evento.EventoId is not null
            && evento.VersionContrato == VersionContratoSoportada
            && configurado is not null
            && evento.RestauranteId == configurado;
    }
}
