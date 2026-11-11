using GIRA.Domain.Base;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Reserva de una mesa para una fecha y franja horaria determinadas.
/// No se elimina nunca; una cancelación se registra en la propia reserva (<see cref="MotivoCancelacion"/>).
/// </summary>
/// <remarks>
/// Transiciones válidas: Confirmada -> EnCurso -> Completada, Confirmada -> Cancelada y
/// Confirmada -> NoAsistio. Cualquier otra combinación lanza <see cref="TransicionReservaInvalidaException"/>.
/// No hay historial de estados: solo se guarda el estado actual.
/// </remarks>
public class Reserva : BaseEntity
{
    /// <summary>Código único de 8 caracteres con el que el cliente consulta o cancela su reserva.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Cliente que hizo la reserva.</summary>
    public Guid ClienteId { get; set; }

    /// <summary>Mesa asignada automáticamente a la reserva.</summary>
    public Guid MesaId { get; set; }

    /// <summary>Franja horaria reservada.</summary>
    public Guid FranjaHorariaId { get; set; }

    /// <summary>Fecha de la reserva, en hora local de República Dominicana.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Cantidad de personas para las que se reservó.</summary>
    public int Personas { get; set; }

    /// <summary>Estado actual de la reserva. Solo cambia a través de los métodos de transición.</summary>
    public EstadoReserva Estado { get; private set; }

    /// <summary>Canal por el que se registró la reserva.</summary>
    public OrigenReserva Origen { get; set; }

    /// <summary>Motivo de la cancelación; null si la reserva no fue cancelada o si canceló sin dar motivo.</summary>
    public string? MotivoCancelacion { get; private set; }

    /// <summary>Cantidad real de personas que llegaron, registrada al hacer check-in.</summary>
    public int? PersonasReales { get; private set; }

    /// <summary>Fecha y hora, en UTC, en que se hizo el check-in; null si no se ha hecho.</summary>
    public DateTime? FechaCheckIn { get; private set; }

    /// <summary>
    /// Hace check-in: pasa de Confirmada a EnCurso y guarda cuántas personas llegaron
    /// en realidad y a qué hora.
    /// </summary>
    public void HacerCheckIn(int personasReales, DateTime ahora)
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new TransicionReservaInvalidaException(
                $"No se puede hacer check-in de una reserva en estado {Estado}; debe estar Confirmada.");
        }

        Estado = EstadoReserva.EnCurso;
        PersonasReales = personasReales;
        FechaCheckIn = ahora;
    }

    /// <summary>
    /// Modifica la fecha, la franja, la mesa y las personas de la reserva (GI-160, RN-RES-05). Solo
    /// una reserva Confirmada puede modificarse y sigue Confirmada. La disponibilidad de la mesa y la
    /// regla de tiempo las valida el servicio que llama a este método, no la entidad.
    /// </summary>
    public void Modificar(DateOnly fecha, Guid franjaHorariaId, Guid mesaId, int personas)
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new TransicionReservaInvalidaException(
                $"No se puede modificar una reserva en estado {Estado}; debe estar Confirmada.");
        }

        Fecha = fecha;
        FranjaHorariaId = franjaHorariaId;
        MesaId = mesaId;
        Personas = personas;
    }

    /// <summary>Completa la visita: pasa de EnCurso a Completada.</summary>
    public void Completar()
    {
        if (Estado != EstadoReserva.EnCurso)
        {
            throw new TransicionReservaInvalidaException(
                $"No se puede completar una reserva en estado {Estado}; debe estar EnCurso.");
        }

        Estado = EstadoReserva.Completada;
    }

    /// <summary>
    /// Cancela la reserva. El motivo es opcional: el cliente puede cancelar sin darlo
    /// (GI-159); si el personal está obligado a darlo, esa validación la hace el
    /// servicio que llama a este método (GI-223), no la entidad.
    /// </summary>
    public void Cancelar(string? motivo)
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new TransicionReservaInvalidaException(
                $"No se puede cancelar una reserva en estado {Estado}; debe estar Confirmada.");
        }

        Estado = EstadoReserva.Cancelada;
        MotivoCancelacion = motivo;
    }

    /// <summary>Marca la reserva como NoAsistio porque el cliente no llegó dentro de la tolerancia.</summary>
    public void MarcarNoAsistio()
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new TransicionReservaInvalidaException(
                $"No se puede marcar NoAsistio una reserva en estado {Estado}; debe estar Confirmada.");
        }

        Estado = EstadoReserva.NoAsistio;
    }
}
