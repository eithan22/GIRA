using System.Security.Claims;
using GIRA.Infrastructure.Identity;

namespace GIRA.Api.Common;

/// <summary>Extensiones para leer datos del usuario autenticado desde sus claims.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario autenticado (claim <see cref="TiposClaim.UsuarioId"/>); null si falta o no es un Guid válido.</summary>
    public static Guid? ObtenerUsuarioId(this ClaimsPrincipal usuario)
    {
        var claim = usuario.FindFirstValue(TiposClaim.UsuarioId);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
