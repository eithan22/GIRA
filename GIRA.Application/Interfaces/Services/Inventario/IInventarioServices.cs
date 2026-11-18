using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;

namespace GIRA.Application.Interfaces.Services.Inventario;

/// <summary>Unidades de medida, categorías de insumo y de plato, y proveedores (RF-INV-02, RF-INV-13).</summary>
public interface ICatalogoInventarioService
{
    /// <summary>Unidades por nombre.</summary>
    Task<OperationResult<IReadOnlyList<UnidadMedidaDto>>> ListarUnidadesAsync(CancellationToken cancellationToken);
    /// <summary>Crea una unidad. 409 si el nombre o la abreviatura ya existen.</summary>
    Task<OperationResult<UnidadMedidaDto>> CrearUnidadAsync(GuardarUnidadMedidaRequest request, CancellationToken cancellationToken);
    /// <summary>Edita una unidad. 404 si no existe; 409 si el nombre o la abreviatura ya existen.</summary>
    Task<OperationResult<UnidadMedidaDto>> ActualizarUnidadAsync(Guid id, GuardarUnidadMedidaRequest request, CancellationToken cancellationToken);

    /// <summary>Categorías de insumo por nombre.</summary>
    Task<OperationResult<IReadOnlyList<CategoriaDto>>> ListarCategoriasInsumoAsync(CancellationToken cancellationToken);
    /// <summary>Crea una categoría de insumo. 409 si el nombre ya existe.</summary>
    Task<OperationResult<CategoriaDto>> CrearCategoriaInsumoAsync(GuardarCategoriaRequest request, CancellationToken cancellationToken);
    /// <summary>Edita una categoría de insumo. 404 si no existe; 409 si el nombre ya existe.</summary>
    Task<OperationResult<CategoriaDto>> ActualizarCategoriaInsumoAsync(Guid id, GuardarCategoriaRequest request, CancellationToken cancellationToken);

    /// <summary>Categorías de plato por nombre.</summary>
    Task<OperationResult<IReadOnlyList<CategoriaDto>>> ListarCategoriasPlatoAsync(CancellationToken cancellationToken);
    /// <summary>Crea una categoría de plato. 409 si el nombre ya existe.</summary>
    Task<OperationResult<CategoriaDto>> CrearCategoriaPlatoAsync(GuardarCategoriaRequest request, CancellationToken cancellationToken);
    /// <summary>Edita una categoría de plato. 404 si no existe; 409 si el nombre ya existe.</summary>
    Task<OperationResult<CategoriaDto>> ActualizarCategoriaPlatoAsync(Guid id, GuardarCategoriaRequest request, CancellationToken cancellationToken);

    /// <summary>Proveedores por nombre.</summary>
    Task<OperationResult<IReadOnlyList<ProveedorDto>>> ListarProveedoresAsync(CancellationToken cancellationToken);
    /// <summary>Crea un proveedor. 409 si el nombre ya existe.</summary>
    Task<OperationResult<ProveedorDto>> CrearProveedorAsync(GuardarProveedorRequest request, CancellationToken cancellationToken);
    /// <summary>Edita un proveedor. 404 si no existe; 409 si el nombre ya existe.</summary>
    Task<OperationResult<ProveedorDto>> ActualizarProveedorAsync(Guid id, GuardarProveedorRequest request, CancellationToken cancellationToken);
}

/// <summary>Catálogo de insumos (RF-INV-01, RN-INV-10).</summary>
public interface IInsumoService
{
    /// <summary>Catálogo paginado con stock y nivel.</summary>
    Task<OperationResult<PagedResult<InsumoDto>>> ListarAsync(ListarInsumosRequest request, CancellationToken cancellationToken);
    /// <summary>Cuántos activos hay y cuántos están en nivel bajo o crítico.</summary>
    Task<OperationResult<ResumenInsumosDto>> ResumenAsync(CancellationToken cancellationToken);
    /// <summary>Insumo por id. 404 si no existe.</summary>
    Task<OperationResult<InsumoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Crea un insumo con stock 0. 400 si la categoría, la unidad o el proveedor no sirven; 409 si el nombre ya existe.</summary>
    Task<OperationResult<InsumoDto>> CrearAsync(GuardarInsumoRequest request, CancellationToken cancellationToken);
    /// <summary>Edita un insumo (no su stock). 404, 400 o 409 como al crear.</summary>
    Task<OperationResult<InsumoDto>> ActualizarAsync(Guid id, GuardarInsumoRequest request, CancellationToken cancellationToken);
    /// <summary>Activa o desactiva. 409 si se quiere desactivar y lo usa una receta activa (RN-INV-10).</summary>
    Task<OperationResult<InsumoDto>> CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken);
}

/// <summary>Platos y recetas (RF-INV-03, RF-INV-04).</summary>
public interface IPlatoService
{
    /// <summary>Todos los platos y la suma de demanda de los activos, con el aviso de RN-INV-05 si aplica.</summary>
    Task<OperationResult<ListadoPlatosDto>> ListarAsync(CancellationToken cancellationToken);
    /// <summary>Crea un plato. 400 si la categoría no existe; 409 si el nombre ya existe.</summary>
    Task<OperationResult<PlatoGuardadoDto>> CrearAsync(GuardarPlatoRequest request, CancellationToken cancellationToken);
    /// <summary>Edita un plato. 404, 400 o 409 como al crear.</summary>
    Task<OperationResult<PlatoGuardadoDto>> ActualizarAsync(Guid id, GuardarPlatoRequest request, CancellationToken cancellationToken);
    /// <summary>Receta del plato (Porciones null si no tiene). 404 si el plato no existe.</summary>
    Task<OperationResult<RecetaDto>> ObtenerRecetaAsync(Guid platoId, CancellationToken cancellationToken);
    /// <summary>Reemplaza la receta. 404 si el plato no existe; 400 si un insumo no existe o está inactivo.</summary>
    Task<OperationResult<RecetaDto>> GuardarRecetaAsync(Guid platoId, GuardarRecetaRequest request, CancellationToken cancellationToken);
}

/// <summary>Movimientos, kardex, conteo físico y salidas teóricas (RF-INV-05, 06, 08, 11).</summary>
public interface IMovimientoInventarioService
{
    /// <summary>
    /// Registra una entrada, merma o ajuste y cambia el stock en la misma transacción.
    /// 404 si el insumo no existe; 409 si está inactivo o si el stock quedaría negativo (RN-INV-02).
    /// </summary>
    Task<OperationResult<MovimientoDto>> RegistrarAsync(RegistrarMovimientoRequest request, Guid? usuarioId, CancellationToken cancellationToken);
    /// <summary>Kardex del insumo con el saldo después de cada movimiento. 404 si el insumo no existe.</summary>
    Task<OperationResult<KardexDto>> KardexAsync(KardexRequest request, CancellationToken cancellationToken);
    /// <summary>Genera un ajuste por cada diferencia contra el stock, todo en una transacción. 400 si un insumo no existe o está inactivo.</summary>
    Task<OperationResult<ConteoFisicoResultadoDto>> ConteoFisicoAsync(ConteoFisicoRequest request, Guid? usuarioId, CancellationToken cancellationToken);
    /// <summary>
    /// Registra las salidas teóricas de una visita completada (RF-INV-08). Si una salida pide más de lo que hay,
    /// el stock queda en cero y se abre una alerta de inconsistencia (RN-INV-02).
    /// </summary>
    Task RegistrarSalidasTeoricasAsync(DateOnly fecha, int personas, bool conReserva, CancellationToken cancellationToken);
}

/// <summary>Alertas de reabastecimiento (RF-INV-09, RF-INV-10, RF-INV-14).</summary>
public interface IAlertaInventarioService
{
    /// <summary>Revisa los lotes por vencer y lista las alertas por estado.</summary>
    Task<OperationResult<PagedResult<AlertaInventarioDto>>> ListarAsync(ListarAlertasRequest request, CancellationToken cancellationToken);
    /// <summary>Atiende o descarta. 404 si no existe; 409 si ya no está abierta.</summary>
    Task<OperationResult<AlertaInventarioDto>> CambiarEstadoAsync(Guid id, CambiarEstadoAlertaRequest request, Guid? usuarioId, CancellationToken cancellationToken);
}

/// <summary>Proyección de consumo (RF-INV-07, RF-INV-12).</summary>
public interface IProyeccionConsumoService
{
    /// <summary>Recalcula y devuelve la proyección entre esas fechas (por defecto, 7 días desde hoy).</summary>
    Task<OperationResult<ProyeccionConsumoDto>> ObtenerAsync(ProyeccionRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Recalcula y guarda el consumo proyectado de esas fechas (las pasadas o más allá de 14 días se ignoran)
    /// y revisa las alertas de todos los insumos activos (RN-INV-06, RN-INV-07).
    /// </summary>
    Task RecalcularAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}

/// <summary>
/// Puerto de Inventario hacia Reservas: personas con reserva vigente por fecha, sin leer las entidades
/// de Reservas. Lo implementa el módulo Reservas (igual que <c>IConsultaSalon</c> para Configuración).
/// </summary>
public interface IConsultaReservasInventario
{
    /// <summary>Suma de personas de las reservas vigentes (Confirmada o EnCurso) de cada fecha; las fechas sin reservas no aparecen.</summary>
    Task<IReadOnlyDictionary<DateOnly, int>> PersonasConReservaPorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
