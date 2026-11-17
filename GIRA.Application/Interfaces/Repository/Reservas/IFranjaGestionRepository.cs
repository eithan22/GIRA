using GIRA.Application.Common;
using GIRA.Domain.Entities.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Repository.Reservas;

public interface IFranjaGestionRepository
{
    Task<FranjaHoraria?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>True si [inicio, fin) se solapa con otra franja ACTIVA; las contiguas no se solapan.</summary>
    Task<bool> ExisteSolapamientoAsync(TimeOnly inicio, TimeOnly fin, Guid? excluirId, CancellationToken cancellationToken);
    Task<PagedResult<FranjaHoraria>> ListarAsync(bool? activa, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Todas las franjas no borradas (activas e inactivas), por hora de inicio y luego por Id, sin
    /// paginar y sin seguimiento de cambios (GI-399).
    /// </summary>
    Task<IReadOnlyList<FranjaHoraria>> ListarTodasAsync(CancellationToken cancellationToken);
    void Agregar(FranjaHoraria franja);
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}