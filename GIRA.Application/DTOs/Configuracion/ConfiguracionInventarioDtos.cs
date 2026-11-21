namespace GIRA.Application.DTOs.Configuracion;

/// <summary>Parámetros del restaurante que usa Inventario (SRS 5.6).</summary>
/// <param name="DiasProyeccionInventario">Días de consumo proyectado que se restan al stock para avisar un quiebre (RN-INV-07).</param>
/// <param name="DiasAvisoVencimientoInventario">Días antes del vencimiento de un lote en que se avisa (RN-INV-11).</param>
public sealed record ParametrosInventarioDto(int DiasProyeccionInventario, int DiasAvisoVencimientoInventario);

/// <summary>Cuerpo de <c>PUT /api/configuracion/inventario</c>. Los campos son obligatorios: omitirlos no se guarda como 0.</summary>
public sealed record ActualizarParametrosInventarioRequest(int? DiasProyeccionInventario, int? DiasAvisoVencimientoInventario);
