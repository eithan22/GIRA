using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas de <see cref="Reserva"/> que necesita el módulo Reservas.</summary>
public interface IReservaRepository
{
    /// <summary>Reservas vigentes (Confirmada o EnCurso) en una fecha, sin importar la franja.</summary>
    Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken);
}
