namespace GIRA.Domain.Entities.Personal;

/// <summary>Jornada real de trabajo registrada por el usuario autenticado.</summary>
public sealed class MarcacionTurno
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTimeOffset InicioEn { get; set; }
    public DateTimeOffset? FinEn { get; set; }
    public int MinutosPausa { get; set; }
}
