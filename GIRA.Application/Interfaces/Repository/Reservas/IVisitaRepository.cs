using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Escrituras de <see cref="VisitaSinReserva"/> que necesita el módulo Reservas (GI-166).</summary>
public interface IVisitaRepository
{
    /// <summary>Deja la visita en seguimiento; no guarda hasta llamar a <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(VisitaSinReserva visita);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Visitas de un día (GI-402): los totales de todo el día y hasta <paramref name="limite"/> filas, las más
    /// recientes primero. Franja y mesa se unen con left join, así una visita cuya franja o mesa ya no existe sigue
    /// en la lista y en los totales.
    /// </summary>
    Task<VisitasDelDiaLectura> ListarPorFechaAsync(DateOnly fecha, int limite, CancellationToken cancellationToken);
}
