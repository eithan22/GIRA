namespace GIRA.Application.Common;

/// <summary>Respuesta paginada de un listado.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
