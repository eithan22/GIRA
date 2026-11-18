using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>
/// Abre y cierra alertas (RN-INV-07, 08 y 09). Solo agrega o cambia alertas en el contexto: quien lo llama
/// guarda y después llama a <see cref="PublicarAsync"/>.
/// </summary>
public class EvaluadorAlertasInventario
{
    private readonly IAlertaInventarioRepository _alertas;
    private readonly CalculadoraConsumo _calculadora;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el evaluador.</summary>
    public EvaluadorAlertasInventario(
        IAlertaInventarioRepository alertas,
        CalculadoraConsumo calculadora,
        IPublicadorEventosSeguro publicador,
        TimeProvider reloj)
    {
        _alertas = alertas;
        _calculadora = calculadora;
        _publicador = publicador;
        _reloj = reloj;
    }

    /// <summary>
    /// Abre <see cref="TipoAlertaInventario.StockBajo"/> si el stock está en el punto de reorden o por debajo; si no,
    /// <see cref="TipoAlertaInventario.QuiebreProyectado"/> si el stock menos el consumo de los próximos días baja
    /// del punto de reorden. Nunca abre una segunda alerta abierta del mismo tipo (RN-INV-08). Ignora inactivos.
    /// </summary>
    public async Task<IReadOnlyList<AlertaInventario>> EvaluarAsync(IReadOnlyCollection<Insumo> insumos, CancellationToken cancellationToken)
    {
        var activos = insumos.Where(i => i.Activo).ToList();
        if (activos.Count == 0)
        {
            return [];
        }

        var proyeccion = await ProyeccionAlertasAsync(cancellationToken);
        var abiertas = await AbiertasAsync(activos.Select(i => i.Id).ToList(), cancellationToken);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var nuevas = new List<AlertaInventario>();

        foreach (var insumo in activos)
        {
            bool YaAbierta(TipoAlertaInventario tipo) => abiertas.Any(a => a.InsumoId == insumo.Id && a.Tipo == tipo);

            var restantes = proyeccion.Restantes(insumo.Id, insumo.CantidadStock);
            var proyectado = restantes[^1];

            if (insumo.CantidadStock <= insumo.PuntoReorden)
            {
                if (!YaAbierta(TipoAlertaInventario.StockBajo))
                {
                    nuevas.Add(Nueva(insumo.Id, TipoAlertaInventario.StockBajo, insumo.CantidadStock, ahora, proyectado));
                }
            }
            else if (proyectado < insumo.PuntoReorden && !YaAbierta(TipoAlertaInventario.QuiebreProyectado))
            {
                var dia = proyeccion.Dias[restantes.ToList().FindIndex(r => r < insumo.PuntoReorden)].Fecha;
                nuevas.Add(Nueva(insumo.Id, TipoAlertaInventario.QuiebreProyectado, insumo.CantidadStock, ahora, proyectado, dia));
            }
        }

        foreach (var alerta in nuevas)
        {
            _alertas.Agregar(alerta);
        }

        return nuevas;
    }

    /// <summary>
    /// RN-INV-09: si después de una entrada el stock proyectado queda sobre el punto de reorden, cierra como Atendidas
    /// (por el sistema) las alertas abiertas de stock bajo y quiebre proyectado del insumo. Devuelve cuántas cerró.
    /// </summary>
    public async Task<int> CerrarPorEntradaAsync(Insumo insumo, CancellationToken cancellationToken)
    {
        var proyeccion = await ProyeccionAlertasAsync(cancellationToken);
        if (proyeccion.Restantes(insumo.Id, insumo.CantidadStock)[^1] <= insumo.PuntoReorden)
        {
            return 0;
        }

        var cerrar = (await AbiertasAsync([insumo.Id], cancellationToken))
            .Where(a => a.Tipo is TipoAlertaInventario.StockBajo or TipoAlertaInventario.QuiebreProyectado)
            .ToList();
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        foreach (var alerta in cerrar)
        {
            alerta.Cerrar(EstadoAlertaInventario.Atendida, usuarioId: null, ahora);
        }

        return cerrar.Count;
    }

    /// <summary>Abre una alerta de <paramref name="tipo"/> si el insumo no tiene otra abierta de ese tipo; si la tiene, devuelve null.</summary>
    public async Task<AlertaInventario?> AbrirSiNoHayAsync(
        Guid insumoId, decimal stockActual, TipoAlertaInventario tipo, string detalle, CancellationToken cancellationToken)
    {
        var abiertas = await AbiertasAsync([insumoId], cancellationToken);
        if (abiertas.Any(a => a.Tipo == tipo))
        {
            return null;
        }

        var alerta = Nueva(insumoId, tipo, stockActual, _reloj.GetUtcNow().UtcDateTime, proyectado: null);
        alerta.Detalle = detalle;
        _alertas.Agregar(alerta);
        return alerta;
    }

    /// <summary>Publica <see cref="AlertaInventarioGeneradaEvent"/> por cada alerta ya guardada.</summary>
    public async Task PublicarAsync(IEnumerable<AlertaInventario> alertas, CancellationToken cancellationToken)
    {
        foreach (var alerta in alertas)
        {
            await _publicador.PublicarAsync(
                new AlertaInventarioGeneradaEvent(alerta.Id, alerta.InsumoId, alerta.Tipo, alerta.StockActual, alerta.StockProyectado, alerta.FechaQuiebreEstimada),
                $"AlertaInventarioId={alerta.Id}",
                cancellationToken);
        }
    }

    // Se vuelve a filtrar en memoria: una alerta cerrada en esta misma operación todavía figura abierta en la base.
    private async Task<List<AlertaInventario>> AbiertasAsync(IReadOnlyCollection<Guid> insumoIds, CancellationToken cancellationToken) =>
        (await _alertas.ListarAbiertasAsync(insumoIds, cancellationToken))
            .Where(a => a.Estado == EstadoAlertaInventario.Abierta)
            .ToList();

    private Task<BaseProyeccion> ProyeccionAlertasAsync(CancellationToken cancellationToken)
    {
        var hoy = _reloj.HoyLocal();
        return _calculadora.CalcularAsync(hoy, hoy.AddDays(ReglasInventario.DiasProyeccionAlertas - 1), cancellationToken);
    }

    private static AlertaInventario Nueva(
        Guid insumoId, TipoAlertaInventario tipo, decimal stock, DateTime ahora, decimal? proyectado, DateOnly? fechaQuiebre = null) => new()
    {
        Id = Guid.NewGuid(),
        InsumoId = insumoId,
        Tipo = tipo,
        StockActual = stock,
        StockProyectado = proyectado,
        FechaQuiebreEstimada = fechaQuiebre,
        FechaGeneracion = ahora
    };
}
