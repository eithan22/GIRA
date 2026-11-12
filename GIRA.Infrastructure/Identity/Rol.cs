using Microsoft.AspNetCore.Identity;

namespace GIRA.Infrastructure.Identity;

/// <summary>
/// Rol del sistema GIRA. Extiende el rol de ASP.NET Core Identity.
/// Los nombres de los roles fijos están en <see cref="GIRA.Domain.Constants.Roles"/>.
/// </summary>
public class Rol : IdentityRole<Guid>
{
    /// <summary>Descripción legible del rol y sus responsabilidades; null si no se definió.</summary>
    public string? Descripcion { get; set; }
}
