using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a los cargos del personal (esquema <c>personal</c>).</summary>
public interface ICargoRepository
{
    /// <summary>Lista los cargos vigentes ordenados por nombre, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken);

    /// <summary>Busca un cargo por su identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Indica si ya existe un cargo vigente con ese nombre, ignorando el cargo <paramref name="excluirId"/>.</summary>
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Agrega un cargo nuevo al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Cargo cargo);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
