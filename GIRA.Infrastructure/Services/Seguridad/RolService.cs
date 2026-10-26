using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Roles;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>Implementación de <see cref="IRolService"/> sobre las tablas de roles de Identity.</summary>
public class RolService : IRolService
{
    private readonly GiraDbContext _db;

    /// <summary>Crea el servicio con el contexto de datos.</summary>
    public RolService(GiraDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<RolDto>>> ListarAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RolDto(r.Id, r.Name!, r.Descripcion))
            .ToListAsync(ct);

        return OperationResult.Success<IReadOnlyList<RolDto>>(roles);
    }
}
