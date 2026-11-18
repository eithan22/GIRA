using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a los requerimientos de personal por cargo (esquema <c>personal</c>).</summary>
public interface IRequerimientoPersonalRepository
{
    /// <summary>Lista los requerimientos, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<RequerimientoPersonal>> ListarAsync(CancellationToken cancellationToken);

    /// <summary>Busca el requerimiento de un cargo, con seguimiento de cambios; null si el cargo no tiene.</summary>
    Task<RequerimientoPersonal?> BuscarPorCargoAsync(Guid cargoId, CancellationToken cancellationToken);

    /// <summary>Agrega un requerimiento nuevo; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(RequerimientoPersonal requerimiento);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
