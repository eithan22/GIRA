using GIRA.Application;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Seguridad.Usuarios;

[Trait("Category", "Integration")]
public class EliminarUsuarioSqlServerTests
{
    [Fact]
    public async Task Eliminar_PersisteEnSqlServer_ConservaHistorialYRevocaSesion()
    {
        var secretos = new ConfigurationBuilder()
            .AddUserSecrets<EliminarUsuarioSqlServerTests>()
            .AddEnvironmentVariables()
            .Build();
        var conexion = secretos.GetConnectionString("GiraGi400Test");
        Assert.False(string.IsNullOrWhiteSpace(conexion));
        Assert.Equal("GIRA_GI400_TEST", new SqlConnectionStringBuilder(conexion).InitialCatalog);

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GiraDb"] = conexion,
            ["Jwt:Issuer"] = "GIRA.Tests",
            ["Jwt:Audience"] = "GIRA.Tests",
            ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
            ["Jwt:AccessTokenMinutos"] = "30",
            ["Jwt:RefreshTokenDias"] = "7"
        }).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthentication();
        servicios.AddApplication();
        servicios.AddInfrastructure(configuracion);

        using var proveedor = servicios.BuildServiceProvider();
        Guid usuarioId;
        Guid registroId;
        string refreshToken;
        string tokenHash;

        using (var scope = proveedor.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            await db.Database.MigrateAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
            if (!await roles.RoleExistsAsync(Roles.Administrador))
            {
                var rolCreado = await roles.CreateAsync(new Rol { Name = Roles.Administrador });
                Assert.True(rolCreado.Succeeded);
            }

            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var adminCorreo = $"gi400-admin-{Guid.NewGuid():N}@gira.local";
            var admin = new Usuario { UserName = adminCorreo, Email = adminCorreo, NombreCompleto = "Admin de prueba", Activo = true };
            Assert.True((await usuarios.CreateAsync(admin, "Prueba-Segura123!")).Succeeded);
            Assert.True((await usuarios.AddToRoleAsync(admin, Roles.Administrador)).Succeeded);

            var correo = $"gi400-usuario-{Guid.NewGuid():N}@gira.local";
            var usuario = new Usuario { UserName = correo, Email = correo, NombreCompleto = "Usuario de prueba", Activo = true };
            Assert.True((await usuarios.CreateAsync(usuario, "Prueba-Segura123!")).Succeeded);
            usuarioId = usuario.Id;

            var registro = new RegistroAcceso
            {
                Id = Guid.NewGuid(), UsuarioId = usuarioId, CorreoIntentado = correo,
                Fecha = DateTime.UtcNow, Ip = "127.0.0.1", Exitoso = true
            };
            registroId = registro.Id;
            db.RegistrosAcceso.Add(registro);
            await db.SaveChangesAsync();

            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            refreshToken = (await tokens.CrearRefreshTokenAsync(usuarioId, "127.0.0.1")).Token;
            tokenHash = tokens.CalcularHash(refreshToken);

            var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();
            var resultado = await servicio.EliminarAsync(usuarioId, admin.Id);
            Assert.True(resultado.IsSuccess, resultado.Error);
        }

        using (var scope = proveedor.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            Assert.False(await db.Users.AnyAsync(u => u.Id == usuarioId));
            Assert.True(await db.RegistrosAcceso.AnyAsync(r => r.Id == registroId && r.UsuarioId == usuarioId));
            var token = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokenHash);
            Assert.NotNull(token.FechaRevocacion);

            var rotador = scope.ServiceProvider.GetRequiredService<IRotadorRefreshToken>();
            var renovacion = await rotador.RotarAsync(refreshToken, "127.0.0.1");
            Assert.True(renovacion.IsFailure);
        }
    }
}
