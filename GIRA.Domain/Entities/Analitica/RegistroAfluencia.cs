namespace GIRA.Domain.Entities.Analitica;

/// <summary>Hecho recibido de Reservas que contribuye al historial de afluencia.</summary>
public sealed class RegistroAfluencia
{
    public Guid Id { get; set; }
    public Guid ReferenciaId { get; set; }
    public OrigenRegistroAfluencia Origen { get; set; }
    public TipoRegistroAfluencia Tipo { get; set; }
    public DateOnly Fecha { get; set; }
    public Guid FranjaHorariaId { get; set; }
    public int Personas { get; set; }
}

public enum OrigenRegistroAfluencia
{
    Reserva = 1,
    VisitaSinReserva = 2
}

public enum TipoRegistroAfluencia
{
    VisitaCompletada = 1,
    ReservaNoAsistida = 2
}
