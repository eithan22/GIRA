namespace GIRA.Application.DTOs.Seguridad.Accesos;

/// <summary>
/// Página del historial de accesos de un usuario, del intento más reciente al más antiguo.
/// Tiene la misma forma que <c>PagedResult&lt;T&gt;</c> de la gestión de usuarios; cuando ese tipo
/// esté en Modulo-Seguridad se puede reemplazar por él.
/// </summary>
/// <param name="Items">Registros de la página pedida.</param>
/// <param name="Total">Total de registros que cumplen los filtros, sin paginar.</param>
/// <param name="Page">Página devuelta.</param>
/// <param name="PageSize">Tamaño de página usado.</param>
public record HistorialAccesosDto(IReadOnlyList<RegistroAccesoDto> Items, int Total, int Page, int PageSize);
