using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Recalcula y guarda el consumo proyectado, y lo devuelve por insumo y día.</summary>
public class ProyeccionConsumoService : IProyeccionConsumoService
{
    private readonly CalculadoraConsumo _calculadora;
    private readonly IConsumoProyectadoRepository _consumos;
    private readonly IInsumoRepository _insumos;
    private readonly EvaluadorAlertasInventario _evaluador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio.</summary>
    public ProyeccionConsumoService(
        CalculadoraConsumo calculadora,
        IConsumoProyectadoRepository consumos,
        IInsumoRepository insumos,
        EvaluadorAlertasInventario evaluador,
        TimeProvider reloj)
    {
        _calculadora = calculadora;
        _consumos = consumos;
        _insumos = insumos;
        _evaluador = evaluador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<ProyeccionConsumoDto>> ObtenerAsync(ProyeccionRequest request, CancellationToken cancellationToken)
    {
        var hoy = _reloj.HoyLocal();
        var desde = request.Desde ?? hoy;
        var hasta = request.Hasta ?? desde.AddDays(6);
        if (desde < hoy)
        {
            return OperationResult.Failure<ProyeccionConsumoDto>(ErrorType.Validation, "La proyección empieza hoy o después.");
        }

        if (hasta < desde)
        {
            return OperationResult.Failure<ProyeccionConsumoDto>(ErrorType.Validation, "La fecha final no puede ser anterior a la inicial.");
        }

        if (hasta.DayNumber - hoy.DayNumber >= ReglasInventario.DiasMaximosProyeccion)
        {
            return OperationResult.Failure<ProyeccionConsumoDto>(
                ErrorType.Validation, $"La proyección llega como máximo a {ReglasInventario.DiasMaximosProyeccion} días contando hoy.");
        }

        BaseProyeccion proyeccion;
        IReadOnlyList<Insumo> activos;
        try
        {
            // Desde hoy, para que "lo que queda" de cada día descuente también los días anteriores a "desde".
            (proyeccion, activos) = await RecalcularInternoAsync(hoy, hasta, cancellationToken);
        }
        catch (Exception ex) when (ex is InventarioDuplicadoException or InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<ProyeccionConsumoDto>(ErrorType.Conflict, "La proyección se estaba recalculando al mismo tiempo; vuelve a intentarlo.");
        }

        var dias = proyeccion.Dias
            .Where(d => d.Fecha >= desde)
            .Select(d => new ComensalesDiaDto(d.Fecha, d.ConReserva, d.Prediccion, d.Usados))
            .ToList();

        var insumos = activos
            .Where(i => proyeccion.FactorPorInsumo.GetValueOrDefault(i.Id) > 0)
            .OrderBy(i => i.Nombre)
            .Select(i => PorInsumo(i, proyeccion, desde))
            .ToList();

        return OperationResult.Success(new ProyeccionConsumoDto(dias, insumos));
    }

    /// <inheritdoc />
    public async Task RecalcularAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var hoy = _reloj.HoyLocal();
        var inicio = desde < hoy ? hoy : desde;
        var limite = hoy.AddDays(ReglasInventario.DiasMaximosProyeccion - 1);
        var fin = hasta > limite ? limite : hasta;
        if (inicio > fin)
        {
            return;
        }

        await RecalcularInternoAsync(inicio, fin, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AplicarPrediccionAsync(PrediccionAfluenciaGeneradaEvent prediccion, CancellationToken cancellationToken)
    {
        if (prediccion.Hasta < prediccion.Desde)
        {
            return;
        }

        var porFecha = prediccion.Predicciones
            .Where(p => p.Fecha >= prediccion.Desde && p.Fecha <= prediccion.Hasta)
            .GroupBy(p => p.Fecha)
            .ToDictionary(g => g.Key, g => g.Sum(p => Math.Max(0, p.AfluenciaEstimada)));
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        // La predicción nueva reemplaza la anterior en todo el rango (RN-ANA-03): una fecha sin franjas queda en 0.
        foreach (var fila in await _consumos.ListarPrediccionesAsync(prediccion.Desde, prediccion.Hasta, cancellationToken))
        {
            fila.Comensales = porFecha.Remove(fila.Fecha, out var comensales) ? comensales : 0;
            fila.FechaRecepcion = ahora;
        }

        foreach (var (fecha, comensales) in porFecha)
        {
            _consumos.AgregarPrediccion(new AfluenciaPredicha { Id = Guid.NewGuid(), Fecha = fecha, Comensales = comensales, FechaRecepcion = ahora });
        }

        await _consumos.GuardarCambiosAsync(cancellationToken);
        await RecalcularAsync(prediccion.Desde, prediccion.Hasta, cancellationToken);
    }

    private async Task<(BaseProyeccion Proyeccion, IReadOnlyList<Insumo> Activos)> RecalcularInternoAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var proyeccion = await _calculadora.CalcularAsync(desde, hasta, cancellationToken);
        var existentes = (await _consumos.ListarAsync(desde, hasta, cancellationToken)).ToDictionary(c => (c.Fecha, c.InsumoId));
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        foreach (var dia in proyeccion.Dias)
        {
            foreach (var insumoId in proyeccion.FactorPorInsumo.Keys)
            {
                var cantidad = proyeccion.Consumo(insumoId, dia);
                if (existentes.Remove((dia.Fecha, insumoId), out var fila))
                {
                    fila.CantidadEsperada = cantidad;
                    fila.ComensalesBase = dia.Usados;
                    fila.FechaCalculo = ahora;
                }
                else
                {
                    _consumos.Agregar(new ConsumoProyectado
                    {
                        Id = Guid.NewGuid(),
                        Fecha = dia.Fecha,
                        InsumoId = insumoId,
                        CantidadEsperada = cantidad,
                        ComensalesBase = dia.Usados,
                        FechaCalculo = ahora
                    });
                }
            }
        }

        // Insumos que ya no están en ninguna receta activa: su consumo esperado pasa a cero.
        foreach (var fila in existentes.Values)
        {
            fila.CantidadEsperada = 0;
            fila.FechaCalculo = ahora;
        }

        var activos = await _insumos.ListarActivosAsync(cancellationToken);
        var nuevas = await _evaluador.EvaluarAsync(activos, cancellationToken);
        await _consumos.GuardarCambiosAsync(cancellationToken);
        await _evaluador.PublicarAsync(nuevas, cancellationToken);

        return (proyeccion, activos);
    }

    private static ProyeccionInsumoDto PorInsumo(Insumo insumo, BaseProyeccion proyeccion, DateOnly desde)
    {
        var restante = insumo.CantidadStock;
        DateOnly? bajoReorden = null, agotamiento = null;
        var dias = new List<ConsumoDiaDto>();

        foreach (var dia in proyeccion.Dias)
        {
            var cantidad = proyeccion.Consumo(insumo.Id, dia);
            restante -= cantidad;
            bajoReorden ??= restante < insumo.PuntoReorden ? dia.Fecha : null;
            agotamiento ??= restante <= 0 && cantidad > 0 ? dia.Fecha : null;
            if (dia.Fecha >= desde)
            {
                dias.Add(new ConsumoDiaDto(dia.Fecha, cantidad, restante));
            }
        }

        return new ProyeccionInsumoDto(
            insumo.Id, insumo.Nombre, insumo.UnidadMedida?.Abreviatura ?? string.Empty,
            insumo.CantidadStock, insumo.PuntoReorden, dias, bajoReorden, agotamiento);
    }
}
