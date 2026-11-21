using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace GIRA.Infrastructure.Persistence.Seed;

/// <summary>
/// Siembra los datos iniciales de seguridad: los 5 roles fijos del sistema y el
/// usuario Administrador. Es idempotente: se puede ejecutar en cada arranque de la
/// aplicación sin duplicar roles ni crear un segundo administrador.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>Crea los roles fijos y el administrador inicial si todavía no existen.</summary>
    public static async Task SeedAsync(RoleManager<Rol> roleManager, UserManager<Usuario> userManager, IConfiguration configuration)
    {
        foreach (var nombreRol in Roles.Todos)
        {
            if (await roleManager.RoleExistsAsync(nombreRol))
            {
                continue;
            }

            var resultadoRol = await roleManager.CreateAsync(new Rol { Name = nombreRol });
            if (!resultadoRol.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el rol '{nombreRol}': {string.Join(", ", resultadoRol.Errors.Select(e => e.Description))}");
            }
        }

        var adminEmail = configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException(
                "Falta 'Seed:AdminEmail' en la configuración. Configúralo en User Secrets.");
        var adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Falta 'Seed:AdminPassword' en la configuración. Configúralo en User Secrets.");

        var administrador = await userManager.FindByEmailAsync(adminEmail);
        if (administrador is null)
        {
            administrador = new Usuario
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                NombreCompleto = "Administrador",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            var resultadoUsuario = await userManager.CreateAsync(administrador, adminPassword);
            if (!resultadoUsuario.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario administrador: {string.Join(", ", resultadoUsuario.Errors.Select(e => e.Description))}");
            }
        }

        if (await userManager.IsInRoleAsync(administrador, Roles.Administrador))
        {
            return;
        }

        var resultadoRolAdmin = await userManager.AddToRoleAsync(administrador, Roles.Administrador);
        if (!resultadoRolAdmin.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar el rol Administrador al usuario administrador: {string.Join(", ", resultadoRolAdmin.Errors.Select(e => e.Description))}");
        }
    }
}
