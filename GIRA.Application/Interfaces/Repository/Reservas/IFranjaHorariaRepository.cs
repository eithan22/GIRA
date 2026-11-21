using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas de <see cref="FranjaHoraria"/> que necesita el módulo Reservas.</summary>
public interface IFranjaHorariaRepository
{
    /// <summary>Franjas horarias activas.</summary>
    Task<IReadOnlyList<FranjaHoraria>> ListarActivasAsync(CancellationToken cancellationToken);

    /// <summary>Busca una franja por su Id, esté activa o no. Null si no existe.</summary>
    Task<FranjaHoraria?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
}
