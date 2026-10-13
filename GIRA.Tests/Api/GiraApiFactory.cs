using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Api;

/// <summary>
/// Levanta la API completa en memoria para pruebas de integración (WebApplicationFactory):
/// mismo Program.cs y pipeline HTTP, pero con una base en memoria propia de cada instancia
/// y sin migraciones ni seeder.
/// </summary>
public class GiraApiFactory : WebApplicationFactory<Program>
{
    private readonly string _nombreBase = Guid.NewGuid().ToString();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Fuera de Development, Program.cs no aplica migraciones ni ejecuta el seeder.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:GiraDb", "Server=no-se-usa");
        builder.UseSetting("Jwt:Key", "clave-de-pruebas-de-gira-con-mas-de-32-caracteres");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<GiraDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GiraDbContext>>();
            services.AddDbContext<GiraDbContext>(options => options.UseInMemoryDatabase(_nombreBase));
        });
    }

    /// <summary>Crea los roles fijos y un usuario con la contraseña y los roles indicados.</summary>
    public async Task CrearUsuarioAsync(string correo, string password, bool activo, params string[] roles)
    {
        using var scope = Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        foreach (var rol in Roles.Todos)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new Rol { Name = rol });
            }
        }

        var usuario = new Usuario { UserName = correo, Email = correo, NombreCompleto = "Ana Pérez", Activo = activo };
        var creado = await userManager.CreateAsync(usuario, password);
        if (!creado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", creado.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRolesAsync(usuario, roles);
    }
}
