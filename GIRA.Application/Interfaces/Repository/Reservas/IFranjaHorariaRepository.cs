using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas de <see cref="FranjaHoraria"/> que necesita el módulo Reservas.</summary>
public interface IFranjaHorariaRepository
{
    /// <summary>Franjas horarias activas.</summary>
    Task<IReadOnlyList<FranjaHoraria>> ListarActivasAsync(CancellationToken cancellationToken);
}
