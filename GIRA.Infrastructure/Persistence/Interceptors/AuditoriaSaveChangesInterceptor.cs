using GIRA.Domain.Base;
using GIRA.Infrastructure.Services.Seguridad.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GIRA.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Rellena la auditoría (CreatedAt/CreatedBy/UpdatedAt/UpdatedBy) y convierte el borrado
/// físico en borrado lógico para toda entidad que herede de <see cref="BaseEntity"/>.
/// </summary>
/// <remarks>
/// CreatedBy/UpdatedBy quedan en null cuando no hay un usuario autenticado en la solicitud
/// actual (clientes sin cuenta, seeders, procesos en segundo plano).
/// </remarks>
public class AuditoriaSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Crea el interceptor con acceso al usuario autenticado de la solicitud actual.</summary>
    public AuditoriaSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AplicarAuditoria(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AplicarAuditoria(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AplicarAuditoria(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var ahora = DateTime.UtcNow;
        var usuarioId = ObtenerUsuarioIdActual();

        foreach (var entrada in contexto.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.Entity.CreatedAt = ahora;
                    entrada.Entity.CreatedBy = usuarioId;
                    break;

                case EntityState.Modified:
                    entrada.Entity.UpdatedAt = ahora;
                    entrada.Entity.UpdatedBy = usuarioId;
                    break;

                case EntityState.Deleted:
                    entrada.State = EntityState.Modified;
                    entrada.Entity.IsDeleted = true;
                    entrada.Entity.DeletedAt = ahora;
                    entrada.Entity.DeletedBy = usuarioId;
                    break;
            }
        }
    }

    private Guid? ObtenerUsuarioIdActual()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst(TiposClaim.UsuarioId);
        return Guid.TryParse(claim?.Value, out var id) ? id : null;
    }
}
