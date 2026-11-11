using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Escrituras de <see cref="VisitaSinReserva"/> que necesita el módulo Reservas (GI-166).</summary>
public interface IVisitaRepository
{
    /// <summary>Deja la visita en seguimiento; no guarda hasta llamar a <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(VisitaSinReserva visita);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
