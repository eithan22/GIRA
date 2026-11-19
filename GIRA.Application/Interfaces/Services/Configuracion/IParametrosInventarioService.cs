using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>
/// Parámetros de Inventario guardados en <c>ConfiguracionRestaurante</c>. Inventario los lee por aquí, sin tocar
/// las tablas de Configuración.
/// </summary>
public interface IParametrosInventarioService
{
    /// <summary>Valores vigentes; los por defecto (3 y 2 días) si todavía no hay configuración guardada.</summary>
    Task<ParametrosInventarioDto> ObtenerAsync(CancellationToken cancellationToken);

    /// <summary>Guarda los dos valores. 400 si salen de rango; 409 si otra persona guardó al mismo tiempo.</summary>
    Task<OperationResult<ParametrosInventarioDto>> ActualizarAsync(ActualizarParametrosInventarioRequest request, CancellationToken cancellationToken);
}
