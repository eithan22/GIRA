using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Platos con su porcentaje de demanda y sus recetas.</summary>
public class PlatoService : IPlatoService
{
    private readonly IPlatoRepository _platos;
    private readonly IInsumoRepository _insumos;
    private readonly ICatalogoInventarioRepository _catalogos;
    private readonly IValidator<GuardarPlatoRequest> _validadorPlato;
    private readonly IValidator<GuardarRecetaRequest> _validadorReceta;

    /// <summary>Crea el servicio.</summary>
    public PlatoService(
        IPlatoRepository platos,
        IInsumoRepository insumos,
        ICatalogoInventarioRepository catalogos,
        IValidator<GuardarPlatoRequest> validadorPlato,
        IValidator<GuardarRecetaRequest> validadorReceta)
    {
        _platos = platos;
        _insumos = insumos;
        _catalogos = catalogos;
        _validadorPlato = validadorPlato;
        _validadorReceta = validadorReceta;
    }

    /// <inheritdoc />
    public async Task<OperationResult<ListadoPlatosDto>> ListarAsync(CancellationToken cancellationToken)
    {
        var platos = await _platos.ListarAsync(cancellationToken);
        var (suma, advertencia) = Demanda(platos);
        var items = platos.OrderByDescending(p => p.Activo).ThenBy(p => p.Nombre).Select(ToDto).ToList();
        return OperationResult.Success(new ListadoPlatosDto(items, suma, advertencia));
    }

    /// <inheritdoc />
    public async Task<OperationResult<PlatoGuardadoDto>> CrearAsync(GuardarPlatoRequest request, CancellationToken cancellationToken)
    {
        var plato = new Plato { Id = Guid.NewGuid() };
        var error = await AplicarAsync(plato, request, esNuevo: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        _platos.Agregar(plato);
        return await GuardarPlatoAsync(plato, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PlatoGuardadoDto>> ActualizarAsync(Guid id, GuardarPlatoRequest request, CancellationToken cancellationToken)
    {
        var plato = await _platos.BuscarPorIdAsync(id, cancellationToken);
        if (plato is null)
        {
            return OperationResult.Failure<PlatoGuardadoDto>(ErrorType.NotFound, "El plato no existe.");
        }

        var error = await AplicarAsync(plato, request, esNuevo: false, cancellationToken);
        return error ?? await GuardarPlatoAsync(plato, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<RecetaDto>> ObtenerRecetaAsync(Guid platoId, CancellationToken cancellationToken)
    {
        var plato = await _platos.BuscarPorIdAsync(platoId, cancellationToken);
        return plato is null
            ? OperationResult.Failure<RecetaDto>(ErrorType.NotFound, "El plato no existe.")
            : OperationResult.Success(ToRecetaDto(plato));
    }

    /// <inheritdoc />
    public async Task<OperationResult<RecetaDto>> GuardarRecetaAsync(Guid platoId, GuardarRecetaRequest request, CancellationToken cancellationToken)
    {
        var plato = await _platos.BuscarPorIdAsync(platoId, cancellationToken);
        if (plato is null)
        {
            return OperationResult.Failure<RecetaDto>(ErrorType.NotFound, "El plato no existe.");
        }

        var validacion = await _validadorReceta.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<RecetaDto>(ErrorType.Validation, validacion.ToString());
        }

        var insumos = (await _insumos.BuscarPorIdsAsync(request.Lineas.Select(l => l.InsumoId).ToList(), cancellationToken))
            .ToDictionary(i => i.Id);
        if (insumos.Count != request.Lineas.Count)
        {
            return OperationResult.Failure<RecetaDto>(ErrorType.Validation, "Uno de los insumos de la receta no existe.");
        }

        var inactivo = insumos.Values.FirstOrDefault(i => !i.Activo);
        if (inactivo is not null)
        {
            return OperationResult.Failure<RecetaDto>(ErrorType.Validation, $"{inactivo.Nombre} está inactivo; no se puede usar en una receta.");
        }

        // Receta y líneas nuevas van sin Id: EF las descubre por la navegación y solo las inserta si el Id está vacío.
        var receta = plato.Receta ??= new Receta { PlatoId = plato.Id };
        receta.Porciones = request.Porciones;
        receta.Instrucciones = ReglasInventario.Limpio(request.Instrucciones);

        // Se actualizan las líneas en su lugar en vez de borrar y volver a crear: así un insumo que se queda
        // no choca con el índice único (receta, insumo) de su propia fila borrada.
        foreach (var sobrante in receta.Detalles.Where(d => !d.IsDeleted && request.Lineas.All(l => l.InsumoId != d.InsumoId)).ToList())
        {
            _platos.QuitarLineaReceta(sobrante);
        }

        foreach (var linea in request.Lineas)
        {
            var detalle = receta.Detalles.FirstOrDefault(d => !d.IsDeleted && d.InsumoId == linea.InsumoId);
            if (detalle is null)
            {
                detalle = new RecetaDetalle { InsumoId = linea.InsumoId };
                receta.Detalles.Add(detalle);
            }

            detalle.Cantidad = linea.Cantidad;
            detalle.Insumo = insumos[linea.InsumoId];
        }

        try
        {
            await _platos.GuardarCambiosAsync(cancellationToken);
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<RecetaDto>(ErrorType.Conflict, "La receta cambió mientras la editabas; recárgala e inténtalo de nuevo.");
        }

        return OperationResult.Success(ToRecetaDto(plato));
    }

    private async Task<OperationResult<PlatoGuardadoDto>?> AplicarAsync(Plato plato, GuardarPlatoRequest request, bool esNuevo, CancellationToken cancellationToken)
    {
        var validacion = await _validadorPlato.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PlatoGuardadoDto>(ErrorType.Validation, validacion.ToString());
        }

        var categoria = await _catalogos.BuscarCategoriaPlatoAsync(request.CategoriaPlatoId, cancellationToken);
        if (categoria is null)
        {
            return OperationResult.Failure<PlatoGuardadoDto>(ErrorType.Validation, "La categoría no existe.");
        }

        var nombre = request.Nombre.Trim();
        if (await _platos.ExisteNombreAsync(nombre, esNuevo ? null : plato.Id, cancellationToken))
        {
            return NombreDuplicado();
        }

        plato.Nombre = nombre;
        plato.CategoriaPlatoId = categoria.Id;
        plato.CategoriaPlato = categoria;
        plato.Precio = request.Precio;
        plato.PorcentajeDemanda = request.PorcentajeDemanda;
        plato.Activo = request.Activo;
        return null;
    }

    private async Task<OperationResult<PlatoGuardadoDto>> GuardarPlatoAsync(Plato plato, CancellationToken cancellationToken)
    {
        try
        {
            await _platos.GuardarCambiosAsync(cancellationToken);
        }
        catch (InventarioDuplicadoException)
        {
            return NombreDuplicado();
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<PlatoGuardadoDto>(ErrorType.Conflict, "El plato cambió mientras lo editabas; recárgalo e inténtalo de nuevo.");
        }

        var (suma, advertencia) = Demanda(await _platos.ListarAsync(cancellationToken));
        return OperationResult.Success(new PlatoGuardadoDto(ToDto(plato), suma, advertencia));
    }

    private static (decimal Suma, string? Advertencia) Demanda(IReadOnlyList<Plato> platos)
    {
        var activos = platos.Where(p => p.Activo).ToList();
        var suma = activos.Sum(p => p.PorcentajeDemanda);
        return (suma, ReglasInventario.AdvertenciaDemanda(suma, activos.Count > 0));
    }

    private static OperationResult<PlatoGuardadoDto> NombreDuplicado() =>
        OperationResult.Failure<PlatoGuardadoDto>(ErrorType.Conflict, "Ya existe un plato con ese nombre.");

    private static PlatoDto ToDto(Plato p) => new(
        p.Id, p.Nombre, p.CategoriaPlatoId, p.CategoriaPlato?.Nombre ?? string.Empty, p.Precio, p.PorcentajeDemanda, p.Activo,
        p.Receta?.Detalles.Count(d => !d.IsDeleted) ?? 0);

    private static RecetaDto ToRecetaDto(Plato plato)
    {
        if (plato.Receta is not { } receta)
        {
            return new RecetaDto(plato.Id, plato.Nombre, null, null, []);
        }

        // Las líneas quitadas siguen en memoria marcadas como borradas hasta que se vuelva a leer el plato.
        var lineas = receta.Detalles
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Insumo?.Nombre)
            .Select(d => new RecetaLineaDto(
                d.InsumoId,
                d.Insumo?.Nombre ?? string.Empty,
                d.Insumo?.UnidadMedida?.Abreviatura ?? string.Empty,
                d.Insumo?.Activo ?? false,
                d.Cantidad,
                Math.Round(d.Cantidad / receta.Porciones, 3)))
            .ToList();

        return new RecetaDto(plato.Id, plato.Nombre, receta.Porciones, receta.Instrucciones, lineas);
    }
}
