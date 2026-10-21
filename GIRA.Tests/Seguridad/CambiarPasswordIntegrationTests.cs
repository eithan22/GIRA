
using GIRA.Application;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GIRA.Tests.Seguridad;

/// <summary>
/// GI-146: Pruebas reales de cambio de contraseña con SQL Server.
/// Solo utiliza la base de datos aislada GIRA_GI100_TEST.
/// </summary>
[Trait("Category", "Integration")]
public class CambiarPasswordIntegrationTests
{
    private const string PasswordActual = "Temporal123!";
    private const string PasswordNueva = "NuevaClave456!";

    [Fact]
    public async Task CambiarPassword_Exitoso_ActualizaUsuarioYRevocaTokens()
    {
        // Leer la conexión desde User Secrets.
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<CambiarPasswordIntegrationTests>()
            .Build();

        var conexion = configuracion
            .GetConnectionString("GiraGi100Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        // Protección para evitar modificar otras bases.
        var datos = new SqlConnectionStringBuilder(conexion);

        Assert.Equal(
            "GIRA_GI100_TEST",
            datos.InitialCatalog);

        var configuracionServicios = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GiraDb"] = conexion,
                ["Jwt:Issuer"] = "GIRA.Tests",
                ["Jwt:Audience"] = "GIRA.Tests",
                ["Jwt:Key"] =
                    "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
                ["Jwt:AccessTokenMinutos"] = "30",
                ["Jwt:RefreshTokenDias"] = "7"
            })
            .Build();

        var servicios = new ServiceCollection();

        servicios.AddLogging();
        servicios.AddAuthentication();
        servicios.AddApplication();
        servicios.AddInfrastructure(configuracionServicios);

        using var proveedor = servicios.BuildServiceProvider();
        using var scope = proveedor.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<GiraDbContext>();

        // Solo se permite migrar la base aislada.
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<Usuario>>();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var tokenService = scope.ServiceProvider
            .GetRequiredService<ITokenService>();

        // Crear un usuario exclusivo para la prueba.
        var correo = $"gi100-{Guid.NewGuid():N}@gira.local";

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Prueba GI100",
            Activo = true,
            DebeCambiarPassword = true
        };

        var creacion = await userManager.CreateAsync(
            usuario,
            PasswordActual);

        Assert.True(creacion.Succeeded);

        // Simular dos sesiones activas.
        var token1 = await tokenService
            .CrearRefreshTokenAsync(usuario.Id, "127.0.0.1");

        var token2 = await tokenService
            .CrearRefreshTokenAsync(usuario.Id, "127.0.0.2");

        // Ejecutar el cambio de contraseña.
        var solicitud = new CambiarPasswordRequest
        {
            PasswordActual = PasswordActual,
            PasswordNueva = PasswordNueva
        };

        var resultado = await authService.CambiarPasswordAsync(
            usuario.Id,
            solicitud);

        Assert.True(resultado.IsSuccess);

        // Consultar nuevamente el usuario.
        var actualizado = await userManager.FindByIdAsync(
            usuario.Id.ToString());

        Assert.NotNull(actualizado);

        // La contraseña anterior ya no funciona.
        Assert.False(await userManager.CheckPasswordAsync(
            actualizado!,
            PasswordActual));

        // La nueva contraseña funciona.
        Assert.True(await userManager.CheckPasswordAsync(
            actualizado!,
            PasswordNueva));

        // Ya no se requiere cambiar contraseña.
        Assert.False(actualizado!.DebeCambiarPassword);

        // Comprobar en SQL Server que ambas sesiones
        // quedaron revocadas.
        var hash1 = tokenService.CalcularHash(token1.Token);
        var hash2 = tokenService.CalcularHash(token2.Token);

        var registros = await db.RefreshTokens
            .AsNoTracking()
            .Where(t =>
                t.TokenHash == hash1 ||
                t.TokenHash == hash2)
            .ToListAsync();

        Assert.Equal(2, registros.Count);
        Assert.All(registros, token =>
            Assert.NotNull(token.FechaRevocacion));
    }
}
