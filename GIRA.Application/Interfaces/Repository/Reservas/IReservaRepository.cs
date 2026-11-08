using GIRA.Domain.Entities.Reservas;

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
