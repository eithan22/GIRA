using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas y escrituras de <see cref="Reserva"/> que necesita el módulo Reservas.</summary>
public interface IReservaRepository
{
    /// <summary>Reservas vigentes (Confirmada o EnCurso) en una fecha, sin importar la franja.</summary>
    Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken);

    /// <summary>Cantidad de reservas vigentes (Confirmada o EnCurso) de un teléfono, sin importar si el cliente ya existe.</summary>
    Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken);

    /// <summary>
    /// Busca la reserva con ese código Y ese teléfono (del cliente asociado) a la vez.
    /// Null si no coinciden los dos, sin distinguir cuál falló.
    /// </summary>
    Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken);

    /// <summary>
    /// Agenda del personal (GI-164): reservas con su cliente, mesa, zona y franja, filtradas por
    /// fecha, estado, franja y teléfono exacto (todos opcionales). Orden: fecha descendente,
    /// hora de inicio de la franja y número de mesa.
    /// </summary>
    Task<PagedResult<ReservaListadoDto>> ListarAsync(
        DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono,
        int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Busca la reserva por id, con seguimiento de cambios, para modificarla (GI-165). Null si
    /// no existe. Los cambios se guardan con <see cref="GuardarCambiosAsync"/>.
    /// </summary>
    Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Detalle de una reserva por id (GI-164). Null si no existe.</summary>
    Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True si la mesa tiene una reserva vigente (Confirmada o EnCurso) en esa fecha y franja (GI-166).</summary>
    Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken);

    /// <summary>
    /// Reservas en el <paramref name="estado"/> indicado con fecha hasta <paramref name="fechaMaxima"/>, con seguimiento
    /// de cambios y las horas de inicio y fin de su franja, ordenadas de la más antigua a la más reciente (fecha e inicio
    /// de franja) y limitadas a <paramref name="limite"/> (GI-161 y GI-224). Es el insumo de los procesos automáticos.
    /// </summary>
    Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(
        EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken);

    /// <summary>
    /// Deja de seguir los cambios de la reserva (GI-161): si un guardado falla, evita que el siguiente la reintente
    /// y arrastre el error a las demás reservas del ciclo.
    /// </summary>
    void Descartar(Reserva reserva);

    /// <summary>True si ya existe una reserva con ese código.</summary>
    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken);

    /// <summary>Deja la reserva en seguimiento; no guarda hasta llamar a <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Reserva reserva);

    /// <summary>
    /// Guarda los cambios pendientes (incluye al Cliente, si se modificó en el mismo contexto).
    /// Si la mesa ya fue tomada para esa fecha y franja, lanza
    /// <see cref="GIRA.Domain.Exceptions.Reservas.ReservaDuplicadaException"/>.
    /// </summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
