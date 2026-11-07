using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas de <see cref="Mesa"/> que necesita el módulo Reservas.</summary>
public interface IMesaRepository
{
    /// <summary>Mesas activas cuya zona también está activa (RN-RES-21).</summary>
    Task<IReadOnlyList<Mesa>> ListarActivasAsync(CancellationToken cancellationToken);
}
