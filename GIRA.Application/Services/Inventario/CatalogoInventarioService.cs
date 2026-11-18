using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Exceptions.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Unidades, categorías de insumo y de plato, y proveedores, sin nombres repetidos.</summary>
public class CatalogoInventarioService : ICatalogoInventarioService
{
    private readonly ICatalogoInventarioRepository _catalogos;
    private readonly IValidator<GuardarUnidadMedidaRequest> _validadorUnidad;
    private readonly IValidator<GuardarCategoriaRequest> _validadorCategoria;
    private readonly IValidator<GuardarProveedorRequest> _validadorProveedor;

    /// <summary>Crea el servicio.</summary>
    public CatalogoInventarioService(
        ICatalogoInventarioRepository catalogos,
        IValidator<GuardarUnidadMedidaRequest> validadorUnidad,
        IValidator<GuardarCategoriaRequest> validadorCategoria,
        IValidator<GuardarProveedorRequest> validadorProveedor)
    {
        _catalogos = catalogos;
        _validadorUnidad = validadorUnidad;
        _validadorCategoria = validadorCategoria;
        _validadorProveedor = validadorProveedor;
    }

    // ---------- Unidades ----------

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<UnidadMedidaDto>>> ListarUnidadesAsync(CancellationToken cancellationToken) =>
        OperationResult.Success<IReadOnlyList<UnidadMedidaDto>>((await _catalogos.ListarUnidadesAsync(cancellationToken)).Select(ToDto).ToList());

    /// <inheritdoc />
    public Task<OperationResult<UnidadMedidaDto>> CrearUnidadAsync(GuardarUnidadMedidaRequest request, CancellationToken cancellationToken) =>
        GuardarUnidadAsync(null, request, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<UnidadMedidaDto>> ActualizarUnidadAsync(Guid id, GuardarUnidadMedidaRequest request, CancellationToken cancellationToken) =>
        GuardarUnidadAsync(id, request, cancellationToken);

    private async Task<OperationResult<UnidadMedidaDto>> GuardarUnidadAsync(Guid? id, GuardarUnidadMedidaRequest request, CancellationToken cancellationToken)
    {
        var unidad = id is null ? new UnidadMedida { Id = Guid.NewGuid() } : await _catalogos.BuscarUnidadAsync(id.Value, cancellationToken);
        if (unidad is null)
        {
            return OperationResult.Failure<UnidadMedidaDto>(ErrorType.NotFound, "La unidad de medida no existe.");
        }

        var validacion = await _validadorUnidad.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<UnidadMedidaDto>(ErrorType.Validation, validacion.ToString());
        }

        const string duplicada = "Ya existe una unidad con ese nombre o esa abreviatura.";
        var (nombre, abreviatura) = (request.Nombre.Trim(), request.Abreviatura.Trim());
        if (await _catalogos.ExisteUnidadAsync(nombre, abreviatura, id, cancellationToken))
        {
            return OperationResult.Failure<UnidadMedidaDto>(ErrorType.Conflict, duplicada);
        }

        unidad.Nombre = nombre;
        unidad.Abreviatura = abreviatura;
        if (id is null)
        {
            _catalogos.AgregarUnidad(unidad);
        }

        return await GuardarAsync(() => ToDto(unidad), duplicada, cancellationToken);
    }

    // ---------- Categorías de insumo ----------

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<CategoriaDto>>> ListarCategoriasInsumoAsync(CancellationToken cancellationToken) =>
        OperationResult.Success<IReadOnlyList<CategoriaDto>>(
            (await _catalogos.ListarCategoriasInsumoAsync(cancellationToken)).Select(c => new CategoriaDto(c.Id, c.Nombre)).ToList());

    /// <inheritdoc />
    public Task<OperationResult<CategoriaDto>> CrearCategoriaInsumoAsync(GuardarCategoriaRequest request, CancellationToken cancellationToken) =>
        GuardarCategoriaAsync(null, request, esDePlato: false, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<CategoriaDto>> ActualizarCategoriaInsumoAsync(Guid id, GuardarCategoriaRequest request, CancellationToken cancellationToken) =>
        GuardarCategoriaAsync(id, request, esDePlato: false, cancellationToken);

    // ---------- Categorías de plato ----------

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<CategoriaDto>>> ListarCategoriasPlatoAsync(CancellationToken cancellationToken) =>
        OperationResult.Success<IReadOnlyList<CategoriaDto>>(
            (await _catalogos.ListarCategoriasPlatoAsync(cancellationToken)).Select(c => new CategoriaDto(c.Id, c.Nombre)).ToList());

    /// <inheritdoc />
    public Task<OperationResult<CategoriaDto>> CrearCategoriaPlatoAsync(GuardarCategoriaRequest request, CancellationToken cancellationToken) =>
        GuardarCategoriaAsync(null, request, esDePlato: true, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<CategoriaDto>> ActualizarCategoriaPlatoAsync(Guid id, GuardarCategoriaRequest request, CancellationToken cancellationToken) =>
        GuardarCategoriaAsync(id, request, esDePlato: true, cancellationToken);

    // Las dos categorías tienen la misma forma (Id, Nombre); solo cambia la tabla.
    private async Task<OperationResult<CategoriaDto>> GuardarCategoriaAsync(Guid? id, GuardarCategoriaRequest request, bool esDePlato, CancellationToken cancellationToken)
    {
        var validacion = await _validadorCategoria.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<CategoriaDto>(ErrorType.Validation, validacion.ToString());
        }

        var nombre = request.Nombre.Trim();
        const string duplicada = "Ya existe una categoría con ese nombre.";
        var existe = esDePlato
            ? await _catalogos.ExisteCategoriaPlatoAsync(nombre, id, cancellationToken)
            : await _catalogos.ExisteCategoriaInsumoAsync(nombre, id, cancellationToken);

        if (esDePlato)
        {
            var categoria = id is null ? new CategoriaPlato { Id = Guid.NewGuid() } : await _catalogos.BuscarCategoriaPlatoAsync(id.Value, cancellationToken);
            if (categoria is null)
            {
                return OperationResult.Failure<CategoriaDto>(ErrorType.NotFound, "La categoría no existe.");
            }

            if (existe)
            {
                return OperationResult.Failure<CategoriaDto>(ErrorType.Conflict, duplicada);
            }

            categoria.Nombre = nombre;
            if (id is null)
            {
                _catalogos.AgregarCategoriaPlato(categoria);
            }

            return await GuardarAsync(() => new CategoriaDto(categoria.Id, categoria.Nombre), duplicada, cancellationToken);
        }
        else
        {
            var categoria = id is null ? new CategoriaInsumo { Id = Guid.NewGuid() } : await _catalogos.BuscarCategoriaInsumoAsync(id.Value, cancellationToken);
            if (categoria is null)
            {
                return OperationResult.Failure<CategoriaDto>(ErrorType.NotFound, "La categoría no existe.");
            }

            if (existe)
            {
                return OperationResult.Failure<CategoriaDto>(ErrorType.Conflict, duplicada);
            }

            categoria.Nombre = nombre;
            if (id is null)
            {
                _catalogos.AgregarCategoriaInsumo(categoria);
            }

            return await GuardarAsync(() => new CategoriaDto(categoria.Id, categoria.Nombre), duplicada, cancellationToken);
        }
    }

    // ---------- Proveedores ----------

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<ProveedorDto>>> ListarProveedoresAsync(CancellationToken cancellationToken) =>
        OperationResult.Success<IReadOnlyList<ProveedorDto>>((await _catalogos.ListarProveedoresAsync(cancellationToken)).Select(ToDto).ToList());

    /// <inheritdoc />
    public Task<OperationResult<ProveedorDto>> CrearProveedorAsync(GuardarProveedorRequest request, CancellationToken cancellationToken) =>
        GuardarProveedorAsync(null, request, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<ProveedorDto>> ActualizarProveedorAsync(Guid id, GuardarProveedorRequest request, CancellationToken cancellationToken) =>
        GuardarProveedorAsync(id, request, cancellationToken);

    private async Task<OperationResult<ProveedorDto>> GuardarProveedorAsync(Guid? id, GuardarProveedorRequest request, CancellationToken cancellationToken)
    {
        var proveedor = id is null ? new Proveedor { Id = Guid.NewGuid() } : await _catalogos.BuscarProveedorAsync(id.Value, cancellationToken);
        if (proveedor is null)
        {
            return OperationResult.Failure<ProveedorDto>(ErrorType.NotFound, "El proveedor no existe.");
        }

        var validacion = await _validadorProveedor.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ProveedorDto>(ErrorType.Validation, validacion.ToString());
        }

        const string duplicado = "Ya existe un proveedor con ese nombre.";
        var nombre = request.Nombre.Trim();
        if (await _catalogos.ExisteProveedorAsync(nombre, id, cancellationToken))
        {
            return OperationResult.Failure<ProveedorDto>(ErrorType.Conflict, duplicado);
        }

        proveedor.Nombre = nombre;
        proveedor.Telefono = TelefonoRd.Normalizar(request.Telefono);
        proveedor.Activo = request.Activo;
        if (id is null)
        {
            _catalogos.AgregarProveedor(proveedor);
        }

        return await GuardarAsync(() => ToDto(proveedor), duplicado, cancellationToken);
    }

    private async Task<OperationResult<T>> GuardarAsync<T>(Func<T> resultado, string mensajeDuplicado, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogos.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success(resultado());
        }
        catch (InventarioDuplicadoException)
        {
            return OperationResult.Failure<T>(ErrorType.Conflict, mensajeDuplicado);
        }
        catch (InventarioConflictoConcurrenciaException)
        {
            return OperationResult.Failure<T>(ErrorType.Conflict, "El registro cambió mientras lo editabas; recárgalo e inténtalo de nuevo.");
        }
    }

    private static UnidadMedidaDto ToDto(UnidadMedida u) => new(u.Id, u.Nombre, u.Abreviatura);

    private static ProveedorDto ToDto(Proveedor p) => new(p.Id, p.Nombre, p.Telefono, p.Activo);
}
