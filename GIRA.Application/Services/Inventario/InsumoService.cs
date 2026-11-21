using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Catálogo de insumos. El stock no se toca aquí: solo con movimientos (RN-INV-01).</summary>
public class InsumoService : IInsumoService
{
    private readonly IInsumoRepository _insumos;
    private readonly ICatalogoInventarioRepository _catalogos;
    private readonly IValidator<GuardarInsumoRequest> _validador;
    private readonly IValidator<ListarInsumosRequest> _validadorListado;

    /// <summary>Crea el servicio.</summary>
    public InsumoService(
        IInsumoRepository insumos,
        ICatalogoInventarioRepository catalogos,
        IValidator<GuardarInsumoRequest> validador,
        IValidator<ListarInsumosRequest> validadorListado)
    {
        _insumos = insumos;
        _catalogos = catalogos;
        _validador = validador;
        _validadorListado = validadorListado;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedResult<InsumoDto>>> ListarAsync(ListarInsumosRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<InsumoDto>>(ErrorType.Validation, validacion.ToString());
        }

        var nivel = new[] { ReglasInventario.NivelNormal, ReglasInventario.NivelBajo, ReglasInventario.NivelCritico }
            .FirstOrDefault(n => string.Equals(n, request.Nivel?.Trim(), StringComparison.OrdinalIgnoreCase));
        var pagina = await _insumos.ListarAsync(
            ReglasInventario.Limpio(request.Buscar), request.Categoria, nivel, request.IncluirInactivos, request.Page, request.PageSize, cancellationToken);

        return OperationResult.Success(new PagedResult<InsumoDto>(pagina.Items.Select(ToDto).ToList(), pagina.Total, pagina.Page, pagina.PageSize));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ResumenInsumosDto>> ResumenAsync(CancellationToken cancellationToken) =>
        OperationResult.Success(await _insumos.ResumenAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<OperationResult<InsumoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var insumo = await _insumos.BuscarPorIdAsync(id, cancellationToken);
        return insumo is null ? NoExiste() : OperationResult.Success(ToDto(insumo));
    }

    /// <inheritdoc />
    public async Task<OperationResult<InsumoDto>> CrearAsync(GuardarInsumoRequest request, CancellationToken cancellationToken)
    {
        var insumo = new Insumo { Id = Guid.NewGuid(), Activo = true };
        var error = await AplicarAsync(insumo, request, esNuevo: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        _insumos.Agregar(insumo);
        return await GuardarAsync(insumo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<InsumoDto>> ActualizarAsync(Guid id, GuardarInsumoRequest request, CancellationToken cancellationToken)
    {
        var insumo = await _insumos.BuscarPorIdAsync(id, cancellationToken);
        if (insumo is null)
        {
            return NoExiste();
        }

        var error = await AplicarAsync(insumo, request, esNuevo: false, cancellationToken);
        return error ?? await GuardarAsync(insumo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<InsumoDto>> CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken)
    {
        var insumo = await _insumos.BuscarPorIdAsync(id, cancellationToken);
        if (insumo is null)
        {
            return NoExiste();
        }

        if (insumo.Activo && !activo)
        {
            var platos = await _insumos.PlatosActivosQueLoUsanAsync(id, cancellationToken);
            if (platos.Count > 0)
            {
                return OperationResult.Failure<InsumoDto>(
                    ErrorType.Conflict,
                    $"No se puede desactivar {insumo.Nombre}: lo usan las recetas activas de {string.Join(", ", platos)}. Quítalo de esas recetas o desactiva los platos primero.");
            }
        }

        insumo.Activo = activo;
        return await GuardarAsync(insumo, cancellationToken);
    }

    // Valida el request y las referencias, y copia los datos al insumo. Devuelve el error o null si todo está bien.
    private async Task<OperationResult<InsumoDto>?> AplicarAsync(Insumo insumo, GuardarInsumoRequest request, bool esNuevo, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<InsumoDto>(ErrorType.Validation, validacion.ToString());
        }

        var categoria = await _catalogos.BuscarCategoriaInsumoAsync(request.CategoriaInsumoId, cancellationToken);
        if (categoria is null)
        {
            return OperationResult.Failure<InsumoDto>(ErrorType.Validation, "La categoría no existe.");
        }

        var unidad = await _catalogos.BuscarUnidadAsync(request.UnidadMedidaId, cancellationToken);
        if (unidad is null)
        {
            return OperationResult.Failure<InsumoDto>(ErrorType.Validation, "La unidad de medida no existe.");
        }

        Proveedor? proveedor = null;
        if (request.ProveedorId is { } proveedorId)
        {
            proveedor = await _catalogos.BuscarProveedorAsync(proveedorId, cancellationToken);
            if (proveedor is null)
            {
                return OperationResult.Failure<InsumoDto>(ErrorType.Validation, "El proveedor no existe.");
            }

            // Si ya lo tenía asignado se deja editar el resto; lo que no se permite es asignar uno inactivo.
            if (!proveedor.Activo && (esNuevo || insumo.ProveedorId != proveedorId))
            {
                return OperationResult.Failure<InsumoDto>(ErrorType.Validation, "No se puede asignar un proveedor inactivo.");
            }
        }

        var nombre = request.Nombre.Trim();
        if (await _insumos.ExisteNombreAsync(nombre, esNuevo ? null : insumo.Id, cancellationToken))
        {
            return NombreDuplicado();
        }

        insumo.Nombre = nombre;
        insumo.CategoriaInsumoId = categoria.Id;
        insumo.CategoriaInsumo = categoria;
        insumo.UnidadMedidaId = unidad.Id;
        insumo.UnidadMedida = unidad;
        insumo.PuntoReorden = request.PuntoReorden;
        insumo.CostoUnitario = request.CostoUnitario;
        insumo.Perecedero = request.Perecedero;
        insumo.ProveedorId = proveedor?.Id;
        insumo.Proveedor = proveedor;
        return null;
    }

    private async Task<OperationResult<InsumoDto>> GuardarAsync(Insumo insumo, CancellationToken cancellationToken)
    {
        try
        {
            await _insumos.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success(ToDto(insumo));
        }
        catch (InventarioDuplicadoException)
        {
            return NombreDuplicado();
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<InsumoDto>(ErrorType.Conflict, "El insumo cambió mientras lo editabas; recárgalo e inténtalo de nuevo.");
        }
    }

    private static OperationResult<InsumoDto> NoExiste() =>
        OperationResult.Failure<InsumoDto>(ErrorType.NotFound, "El insumo no existe.");

    private static OperationResult<InsumoDto> NombreDuplicado() =>
        OperationResult.Failure<InsumoDto>(ErrorType.Conflict, "Ya existe un insumo con ese nombre.");

    private static InsumoDto ToDto(Insumo i) => new(
        i.Id,
        i.Nombre,
        i.CategoriaInsumoId,
        i.CategoriaInsumo?.Nombre ?? string.Empty,
        i.UnidadMedidaId,
        i.UnidadMedida?.Abreviatura ?? string.Empty,
        i.CantidadStock,
        i.PuntoReorden,
        ReglasInventario.Nivel(i.CantidadStock, i.PuntoReorden),
        i.CostoUnitario,
        i.Perecedero,
        i.ProveedorId,
        i.Proveedor?.Nombre,
        i.Activo);
}
