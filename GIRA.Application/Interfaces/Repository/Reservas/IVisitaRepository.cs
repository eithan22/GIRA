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

    /// <summary>
    /// Mesas con una visita sin reserva en esa fecha y franja: cuentan como ocupantes para las reservas, sin
    /// mirar la hora actual (la visita no tiene hora de salida). Las visitas sin mesa no aparecen.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListarMesasConVisitaAsync(DateOnly fecha, Guid franjaId, CancellationToken cancellationToken);

    /// <summary>
    /// Lo mismo que <see cref="ListarMesasConVisitaAsync"/> pero de todo el día, agrupado por franja: una sola
    /// consulta para la disponibilidad, que evalúa todas las franjas de la fecha. Una franja sin visitas no
    /// aparece en el diccionario.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ListarMesasConVisitaPorFechaAsync(
        DateOnly fecha, CancellationToken cancellationToken);
}
