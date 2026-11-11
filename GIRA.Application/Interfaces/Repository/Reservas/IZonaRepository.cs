using GIRA.Application.Common;
using GIRA.Domain.Entities.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Repository.Reservas;

public interface IZonaRepository
{
    Task<Zona?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    Task<PagedResult<Zona>> ListarAsync(bool? activa, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    void Agregar(Zona zona);
    /// <summary>Lanza SalonDuplicadoException o SalonConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
