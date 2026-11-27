using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Configuracion;

/// <summary>Implementación de <see cref="INotificacionRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class NotificacionRepository : INotificacionRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public NotificacionRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public void Agregar(IEnumerable<Notificacion> notificaciones) => _contexto.Notificaciones.AddRange(notificaciones);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notificacion>> ListarDelUsuarioAsync(
        Guid usuarioId, bool? leidas, int cantidad, CancellationToken cancellationToken) =>
        await _contexto.Notificaciones.AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId && (leidas == null || n.Leida == leidas))
            .OrderByDescending(n => n.CreatedAt)
            .Take(cantidad)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> ContarNoLeidasAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        _contexto.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && !n.Leida, cancellationToken);

    /// <inheritdoc />
    public Task<Notificacion?> BuscarDelUsuarioAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken) =>
        _contexto.Notificaciones.FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId, cancellationToken);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => _contexto.SaveChangesAsync(cancellationToken);
}
