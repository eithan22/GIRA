using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Entradas, mermas, ajustes, conteo físico, kardex y salidas teóricas.</summary>
public class MovimientoInventarioService : IMovimientoInventarioService
{
    private readonly IInsumoRepository _insumos;
    private readonly IMovimientoInventarioRepository _movimientos;
    private readonly IPlatoRepository _platos;
    private readonly EvaluadorAlertasInventario _evaluador;
    private readonly IValidator<RegistrarMovimientoRequest> _validadorMovimiento;
    private readonly IValidator<KardexRequest> _validadorKardex;
    private readonly IValidator<ConteoFisicoRequest> _validadorConteo;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio.</summary>
    public MovimientoInventarioService(
        IInsumoRepository insumos,
        IMovimientoInventarioRepository movimientos,
        IPlatoRepository platos,
        EvaluadorAlertasInventario evaluador,
        IValidator<RegistrarMovimientoRequest> validadorMovimiento,
        IValidator<KardexRequest> validadorKardex,
        IValidator<ConteoFisicoRequest> validadorConteo,
        TimeProvider reloj)
    {
        _insumos = insumos;
        _movimientos = movimientos;
        _platos = platos;
        _evaluador = evaluador;
        _validadorMovimiento = validadorMovimiento;
        _validadorKardex = validadorKardex;
        _validadorConteo = validadorConteo;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<MovimientoDto>> RegistrarAsync(RegistrarMovimientoRequest request, Guid? usuarioId, CancellationToken cancellationToken)
    {
        var validacion = await _validadorMovimiento.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<MovimientoDto>(ErrorType.Validation, validacion.ToString());
        }

        var insumo = await _insumos.BuscarPorIdAsync(request.InsumoId, cancellationToken);
        if (insumo is null)
        {
            return OperationResult.Failure<MovimientoDto>(ErrorType.NotFound, "El insumo no existe.");
        }

        if (!insumo.Activo)
        {
            return OperationResult.Failure<MovimientoDto>(ErrorType.Conflict, "El insumo está inactivo; actívalo antes de registrar movimientos.");
        }

        var tipo = ReglasInventario.Leer<TipoMovimientoInventario>(request.Tipo);
        var hoy = _reloj.HoyLocal();
        var lote = tipo == TipoMovimientoInventario.Entrada && insumo.Perecedero;
        if (lote && request.FechaVencimiento is null)
        {
            return OperationResult.Failure<MovimientoDto>(ErrorType.Validation, "Los insumos perecederos necesitan fecha de vencimiento.");
        }

        if (lote && request.FechaVencimiento < hoy)
        {
            return OperationResult.Failure<MovimientoDto>(ErrorType.Validation, "La fecha de vencimiento ya pasó.");
        }

        var variacion = tipo == TipoMovimientoInventario.Merma ? -request.Cantidad : request.Cantidad;
        if (insumo.CantidadStock + variacion < 0)
        {
            return OperationResult.Failure<MovimientoDto>(
                ErrorType.Validation,
                $"El stock quedaría en {ReglasInventario.Texto(insumo.CantidadStock + variacion)} {insumo.UnidadMedida?.Abreviatura}. No puede quedar por debajo de cero.");
        }

        decimal? costo = tipo == TipoMovimientoInventario.Entrada
            ? request.CostoUnitario ?? insumo.CostoUnitario
            : null;
        var movimiento = await AplicarAsync(insumo, tipo, variacion, request.Motivo, request.Referencia, costo, teorico: false, usuarioId, cancellationToken);
        if (lote)
        {
            _insumos.AgregarLote(new LoteInsumo
            {
                Id = Guid.NewGuid(),
                InsumoId = insumo.Id,
                Cantidad = request.Cantidad,
                CostoUnitario = costo,
                FechaEntrada = hoy,
                FechaVencimiento = request.FechaVencimiento!.Value
            });
        }

        if (tipo == TipoMovimientoInventario.Entrada)
        {
            await _evaluador.CerrarPorEntradaAsync(insumo, cancellationToken);
        }

        var nuevas = await _evaluador.EvaluarAsync([insumo], cancellationToken);
        var guardado = await GuardarAsync(cancellationToken);
        if (guardado.IsFailure)
        {
            return OperationResult.Failure<MovimientoDto>(guardado.Type!.Value, guardado.Error!);
        }

        await _evaluador.PublicarAsync(nuevas, cancellationToken);
        return OperationResult.Success(ToDto(movimiento, usuario: null));
    }

    /// <inheritdoc />
    public async Task<OperationResult<KardexDto>> KardexAsync(KardexRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorKardex.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<KardexDto>(ErrorType.Validation, validacion.ToString());
        }

        var insumo = await _insumos.BuscarPorIdAsync(request.InsumoId!.Value, cancellationToken);
        if (insumo is null)
        {
            return OperationResult.Failure<KardexDto>(ErrorType.NotFound, "El insumo no existe.");
        }

        var hasta = request.Hasta ?? _reloj.HoyLocal();
        var desde = request.Desde ?? hasta.AddDays(-29);
        if (desde > hasta)
        {
            return OperationResult.Failure<KardexDto>(ErrorType.Validation, "La fecha inicial no puede ser posterior a la final.");
        }

        var desdeUtc = ReglasInventario.InicioDelDiaUtc(desde);
        var hastaUtc = ReglasInventario.InicioDelDiaUtc(hasta.AddDays(1));
        var saldoInicial = await _movimientos.SaldoAntesDeAsync(insumo.Id, desdeUtc, cancellationToken);
        var pagina = await _movimientos.ListarAsync(insumo.Id, desdeUtc, hastaUtc, request.Page, request.PageSize, cancellationToken);
        var totales = await _movimientos.TotalesPorTipoAsync(insumo.Id, desdeUtc, hastaUtc, cancellationToken);

        var movimientos = new PagedResult<MovimientoDto>(
            pagina.Items.Select(l => ToDto(l.Movimiento, l.Usuario)).ToList(), pagina.Total, pagina.Page, pagina.PageSize);

        return OperationResult.Success(new KardexDto(
            insumo.Id,
            insumo.Nombre,
            insumo.UnidadMedida?.Abreviatura ?? string.Empty,
            insumo.CantidadStock,
            desde,
            hasta,
            saldoInicial,
            totales.GetValueOrDefault(TipoMovimientoInventario.Entrada),
            totales.GetValueOrDefault(TipoMovimientoInventario.Salida),
            totales.GetValueOrDefault(TipoMovimientoInventario.Merma),
            totales.GetValueOrDefault(TipoMovimientoInventario.Ajuste),
            movimientos));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ConteoFisicoResultadoDto>> ConteoFisicoAsync(ConteoFisicoRequest request, Guid? usuarioId, CancellationToken cancellationToken)
    {
        var validacion = await _validadorConteo.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ConteoFisicoResultadoDto>(ErrorType.Validation, validacion.ToString());
        }

        var insumos = (await _insumos.BuscarPorIdsAsync(request.Lineas.Select(l => l.InsumoId).ToList(), cancellationToken))
            .ToDictionary(i => i.Id);
        if (insumos.Count != request.Lineas.Count)
        {
            return OperationResult.Failure<ConteoFisicoResultadoDto>(ErrorType.Validation, "Uno de los insumos contados no existe.");
        }

        var inactivo = insumos.Values.FirstOrDefault(i => !i.Activo);
        if (inactivo is not null)
        {
            return OperationResult.Failure<ConteoFisicoResultadoDto>(ErrorType.Validation, $"{inactivo.Nombre} está inactivo; no se puede contar.");
        }

        var ajustes = new List<MovimientoInventario>();
        var ajustados = new List<Insumo>();
        foreach (var linea in request.Lineas)
        {
            var insumo = insumos[linea.InsumoId];
            var diferencia = linea.CantidadContada - insumo.CantidadStock;
            if (diferencia == 0)
            {
                continue;
            }

            ajustes.Add(await AplicarAsync(insumo, TipoMovimientoInventario.Ajuste, diferencia, request.Motivo, "Conteo físico", null, teorico: false, usuarioId, cancellationToken));
            ajustados.Add(insumo);
        }

        if (ajustes.Count == 0)
        {
            return OperationResult.Success(new ConteoFisicoResultadoDto(0, []));
        }

        var nuevas = await _evaluador.EvaluarAsync(ajustados, cancellationToken);
        var guardado = await GuardarAsync(cancellationToken);
        if (guardado.IsFailure)
        {
            return OperationResult.Failure<ConteoFisicoResultadoDto>(guardado.Type!.Value, guardado.Error!);
        }

        await _evaluador.PublicarAsync(nuevas, cancellationToken);
        return OperationResult.Success(new ConteoFisicoResultadoDto(ajustes.Count, ajustes.Select(m => ToDto(m, usuario: null)).ToList()));
    }

    /// <inheritdoc />
    public async Task<bool> RegistrarSalidasTeoricasAsync(DateOnly fecha, int personas, bool conReserva, Guid visitaId, CancellationToken cancellationToken)
    {
        if (await _movimientos.ExisteDeVisitaAsync(visitaId, cancellationToken))
        {
            return false;
        }

        if (personas <= 0)
        {
            return true;
        }

        var factores = CalculadoraConsumo.Factores(await _platos.ListarActivosConRecetaAsync(cancellationToken));
        if (factores.Count == 0)
        {
            return true;
        }

        var referencia = $"Visita {(conReserva ? "con" : "sin")} reserva del {ReglasInventario.Texto(fecha)}, {personas} personas";
        var insumos = await _insumos.BuscarPorIdsAsync(factores.Keys.ToList(), cancellationToken);
        var afectados = new List<Insumo>();
        var nuevas = new List<AlertaInventario>();

        foreach (var insumo in insumos.Where(i => i.Activo))
        {
            var pedido = Math.Round(factores[insumo.Id] * personas, 3);
            if (pedido <= 0)
            {
                continue;
            }

            var aplicado = Math.Min(pedido, insumo.CantidadStock);
            if (aplicado > 0)
            {
                var salida = await AplicarAsync(insumo, TipoMovimientoInventario.Salida, -aplicado, null, referencia, null, teorico: true, usuarioId: null, cancellationToken);
                salida.VisitaId = visitaId;
                afectados.Add(insumo);
            }

            if (aplicado < pedido)
            {
                // RN-INV-02: una salida teórica no deja el stock negativo; queda en cero y se avisa.
                var unidad = insumo.UnidadMedida?.Abreviatura;
                var alerta = await _evaluador.AbrirSiNoHayAsync(
                    insumo.Id,
                    insumo.CantidadStock,
                    TipoAlertaInventario.Inconsistencia,
                    $"Una salida teórica pedía {ReglasInventario.Texto(pedido)} {unidad} y solo había {ReglasInventario.Texto(aplicado)} {unidad}. El stock quedó en 0; revisa con un conteo físico.",
                    cancellationToken);
                if (alerta is not null)
                {
                    nuevas.Add(alerta);
                }
            }
        }

        nuevas.AddRange(await _evaluador.EvaluarAsync(afectados, cancellationToken));
        try
        {
            // Otro fallo sube al publicador de Reservas, que lo registra en el log (no se pierde en silencio).
            await _insumos.GuardarCambiosAsync(cancellationToken);
        }
        catch (InventarioDuplicadoException)
        {
            // La misma visita se procesó al mismo tiempo en otro lado y guardó primero (índice único de VisitaId).
            return false;
        }

        await _evaluador.PublicarAsync(nuevas, cancellationToken);
        return true;
    }

    private async Task<MovimientoInventario> AplicarAsync(
        Insumo insumo,
        TipoMovimientoInventario tipo,
        decimal variacion,
        string? motivo,
        string? referencia,
        decimal? costo,
        bool teorico,
        Guid? usuarioId,
        CancellationToken cancellationToken)
    {
        insumo.AplicarMovimiento(variacion);
        if (variacion < 0 && insumo.Perecedero)
        {
            var costoLotes = await DescontarDeLotesAsync(insumo.Id, -variacion, cancellationToken);
            if (tipo is TipoMovimientoInventario.Salida or TipoMovimientoInventario.Merma)
                costo = costoLotes;
        }
        else if (variacion < 0 && tipo is TipoMovimientoInventario.Salida or TipoMovimientoInventario.Merma)
            costo = insumo.CostoUnitario;

        var movimiento = new MovimientoInventario
        {
            Id = Guid.NewGuid(),
            InsumoId = insumo.Id,
            Tipo = tipo,
            Cantidad = variacion,
            CostoUnitario = costo,
            Fecha = _reloj.GetUtcNow().UtcDateTime,
            Motivo = ReglasInventario.Limpio(motivo),
            Referencia = ReglasInventario.Limpio(referencia),
            Teorico = teorico,
            UsuarioId = usuarioId,
            SaldoResultante = insumo.CantidadStock
        };
        _movimientos.Agregar(movimiento);
        return movimiento;
    }

    // Las salidas gastan primero el lote que vence antes, para no avisar el vencimiento de algo que ya se usó.
    private async Task<decimal?> DescontarDeLotesAsync(
        Guid insumoId, decimal cantidad, CancellationToken cancellationToken)
    {
        var cantidadOriginal = cantidad;
        var costoTotal = 0m;
        var completo = true;
        foreach (var lote in await _insumos.ListarLotesConSaldoAsync(insumoId, cancellationToken))
        {
            if (cantidad <= 0)
            {
                break;
            }

            var usar = Math.Min(lote.Cantidad, cantidad);
            if (lote.CostoUnitario is decimal costo)
                costoTotal += usar * costo;
            else
                completo = false;
            lote.Cantidad -= usar;
            cantidad -= usar;
        }

        if (cantidad > 0)
            completo = false;

        return completo && cantidadOriginal > 0 ? costoTotal / cantidadOriginal : null;
    }

    private async Task<OperationResult> GuardarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _movimientos.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure(ErrorType.Conflict, "El stock cambió mientras guardabas; revisa el stock actual y vuelve a intentarlo.");
        }
    }

    private static MovimientoDto ToDto(MovimientoInventario m, string? usuario) => new(
        m.Id, m.InsumoId, m.Tipo.ToString(), m.Cantidad, m.CostoUnitario, ReglasInventario.Utc(m.Fecha),
        m.Motivo, m.Referencia, m.Teorico, usuario, m.SaldoResultante);
}
