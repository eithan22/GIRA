using GIRA.Application.Common;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Zonas en memoria para las pruebas sin base de datos. Con zonas sembradas solo existen esas; sin sembrar nada
/// (las pruebas que no tratan de zonas) cualquier id se resuelve a una zona activa "Salón", como si la mesa
/// siempre tuviera su zona.
/// </summary>
class ZonaRepositoryFalso : IZonaRepository
{
    private readonly Dictionary<Guid, Zona> _zonas;
    private readonly bool _permisivo;

    public ZonaRepositoryFalso(params Zona[] zonas)
    {
        _zonas = zonas.ToDictionary(z => z.Id);
        _permisivo = zonas.Length == 0;
    }

    public Task<Zona?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_zonas.TryGetValue(id, out var zona)) return Task.FromResult<Zona?>(zona);
        return Task.FromResult<Zona?>(_permisivo ? new Zona { Id = id, Nombre = "Salón", Activa = true } : null);
    }

    public Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(ids.ToDictionary(
            id => id, id => _zonas.TryGetValue(id, out var z) ? z.Nombre : "Salón"));

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResult<Zona>> ListarAsync(bool? activa, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void Agregar(Zona zona) => throw new NotImplementedException();
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
