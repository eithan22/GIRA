using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Panel de alertas: lista, atiende o descarta, y avisa los lotes por vencer.</summary>
public class AlertaInventarioService : IAlertaInventarioService
{
    private readonly IAlertaInventarioRepository _alertas;
    private readonly IInsumoRepository _insumos;
    private readonly EvaluadorAlertasInventario _evaluador;
    private readonly IValidator<ListarAlertasRequest> _validadorListado;
    private readonly IValidator<CambiarEstadoAlertaRequest> _validadorEstado;
    private readonly IParametrosInventarioService _parametros;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio.</summary>
    public AlertaInventarioService(
        IAlertaInventarioRepository alertas,
        IInsumoRepository insumos,
        EvaluadorAlertasInventario evaluador,
        IValidator<ListarAlertasRequest> validadorListado,
        IValidator<CambiarEstadoAlertaRequest> validadorEstado,
        IParametrosInventarioService parametros,
        TimeProvider reloj)
    {
        _alertas = alertas;
        _insumos = insumos;
        _evaluador = evaluador;
        _validadorListado = validadorListado;
        _validadorEstado = validadorEstado;
        _parametros = parametros;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedResult<AlertaInventarioDto>>> ListarAsync(ListarAlertasRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<AlertaInventarioDto>>(ErrorType.Validation, validacion.ToString());
        }

        await RevisarVencimientosAsync(cancellationToken);

        EstadoAlertaInventario? estado = string.IsNullOrWhiteSpace(request.Estado)
            ? null
            : ReglasInventario.Leer<EstadoAlertaInventario>(request.Estado);
        var pagina = await _alertas.ListarAsync(estado, request.Page, request.PageSize, cancellationToken);

        return OperationResult.Success(new PagedResult<AlertaInventarioDto>(
            pagina.Items.Select(ToDto).ToList(), pagina.Total, pagina.Page, pagina.PageSize));
    }

    /// <inheritdoc />
    public async Task<OperationResult<AlertaInventarioDto>> CambiarEstadoAsync(
        Guid id, CambiarEstadoAlertaRequest request, Guid? usuarioId, CancellationToken cancellationToken)
    {
        var validacion = await _validadorEstado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<AlertaInventarioDto>(ErrorType.Validation, validacion.ToString());
        }

        var alerta = await _alertas.BuscarPorIdAsync(id, cancellationToken);
        if (alerta is null)
        {
            return OperationResult.Failure<AlertaInventarioDto>(ErrorType.NotFound, "La alerta no existe.");
        }

        if (alerta.Estado != EstadoAlertaInventario.Abierta)
        {
            return OperationResult.Failure<AlertaInventarioDto>(ErrorType.Conflict, "La alerta ya fue atendida o descartada.");
        }

        alerta.Cerrar(ReglasInventario.Leer<EstadoAlertaInventario>(request.Estado), usuarioId, _reloj.GetUtcNow().UtcDateTime);
        try
        {
            await _alertas.GuardarCambiosAsync(cancellationToken);
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<AlertaInventarioDto>(ErrorType.Conflict, "Otra persona cambió la alerta al mismo tiempo; recarga la lista.");
        }

        var lectura = await _alertas.ObtenerLecturaAsync(id, cancellationToken);
        return OperationResult.Success(ToDto(lectura!));
    }

    /// <inheritdoc />
    /// <remarks>Lo corre una vez al día el proceso de vencimientos (GI-483) y también al abrir el panel de alertas.</remarks>
    public async Task<int> RevisarVencimientosAsync(CancellationToken cancellationToken)
    {
        var hoy = _reloj.HoyLocal();
        var dias = (await _parametros.ObtenerAsync(cancellationToken)).DiasAvisoVencimientoInventario;
        var lotes = await _insumos.ListarLotesPorVencerAsync(hoy.AddDays(dias), cancellationToken);
        var nuevas = new List<AlertaInventario>();
        // Cada lote avisa una sola vez: si la alerta ya se atendió o descartó, ese lote no vuelve a salir.
        var avisados = (await _alertas.ListarLotesConAlertaAsync(lotes.Select(l => l.LoteId).ToList(), cancellationToken)).ToHashSet();

        foreach (var lote in lotes.Where(l => !avisados.Contains(l.LoteId))
                     .GroupBy(l => l.InsumoId).Select(g => g.OrderBy(l => l.FechaVencimiento).First()))
        {
            var verbo = lote.FechaVencimiento < hoy ? "venció" : "vence";
            var alerta = await _evaluador.AbrirSiNoHayAsync(
                lote.InsumoId,
                lote.StockInsumo,
                TipoAlertaInventario.Vencimiento,
                $"Un lote de {ReglasInventario.Texto(lote.Cantidad)} {lote.Unidad} {verbo} el {ReglasInventario.Texto(lote.FechaVencimiento)}.",
                cancellationToken);
            if (alerta is not null)
            {
                alerta.LoteInsumoId = lote.LoteId;
                nuevas.Add(alerta);
            }
        }

        if (nuevas.Count == 0)
        {
            return 0;
        }

        await _alertas.GuardarCambiosAsync(cancellationToken);
        await _evaluador.PublicarAsync(nuevas, cancellationToken);
        return nuevas.Count;
    }

    private static AlertaInventarioDto ToDto(AlertaLectura lectura)
    {
        var a = lectura.Alerta;
        var insumo = a.Insumo!;
        return new AlertaInventarioDto(
            a.Id,
            a.InsumoId,
            insumo.Nombre,
            insumo.UnidadMedida?.Abreviatura ?? string.Empty,
            insumo.CantidadStock,
            insumo.PuntoReorden,
            a.Tipo.ToString(),
            a.Estado.ToString(),
            a.StockActual,
            a.StockProyectado,
            a.FechaQuiebreEstimada,
            a.Detalle,
            ReglasInventario.Utc(a.FechaGeneracion),
            // Sin usuario la cerró una entrada automática (RN-INV-09).
            a.Estado == EstadoAlertaInventario.Abierta ? null : a.AtendidaPor is null ? "Sistema" : lectura.AtendidaPor,
            a.FechaAtencion is { } fecha ? ReglasInventario.Utc(fecha) : null);
    }
}
