
using GIRA.Application;
using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Seguridad.Auth;

public class RefreshTokenTests : IDisposable
{
    private readonly ServiceProvider _proveedor;
    private readonly IServiceScope _scope;

    public RefreshTokenTests()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GiraDb"] = "Server=no-se-usa",
                ["Jwt:Issuer"] = "GIRA.Tests",
                ["Jwt:Audience"] = "GIRA.Tests",
                ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
                ["Jwt:AccessTokenMinutos"] = "30",
                ["Jwt:RefreshTokenDias"] = "7"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddAuthentication();
        services.AddApplication();
        services.AddInfrastructure(configuracion);

        services.RemoveAll<DbContextOptions<GiraDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<GiraDbContext>>();

        services.AddDbContext<GiraDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _proveedor = services.BuildServiceProvider();
        _scope = _proveedor.CreateScope();
    }

    private IAuthService AuthService =>
        _scope.ServiceProvider.GetRequiredService<IAuthService>();

    private ITokenService TokenService =>
        _scope.ServiceProvider.GetRequiredService<ITokenService>();

    private GiraDbContext DbContext =>
        _scope.ServiceProvider.GetRequiredService<GiraDbContext>();

    public void Dispose()
    {
        _scope.Dispose();
        _proveedor.Dispose();
    }

    [Fact]
    public async Task LogoutAsync_TokenValido_RevocaElToken()
    {
        // Arrange: crear un identificador de usuario y un token.
        var usuarioId = Guid.NewGuid();

        var token = await TokenService.CrearRefreshTokenAsync(
            usuarioId,
            "127.0.0.1");

        // Act: cerrar sesión utilizando el token.
        var resultado = await AuthService.LogoutAsync(
            usuarioId,
            token.Token);

        // Assert: verificar que el resultado sea exitoso.
        Assert.True(resultado.IsSuccess);

        // El token debe tener una fecha de revocación.
        var tokenGuardado = await DbContext.RefreshTokens
            .SingleAsync();

        Assert.NotNull(tokenGuardado.FechaRevocacion);
    }

    [Fact]
    public async Task LogoutAsync_TokenDeOtroUsuario_DevuelveNoAutorizado()
    {
        // Arrange
        var propietarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();

        var token = await TokenService.CrearRefreshTokenAsync(
            propietarioId,
            "127.0.0.1");

        // Act
        var resultado = await AuthService.LogoutAsync(
            otroUsuarioId,
            token.Token);

        // Assert
        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, resultado.Type);

        // El token del propietario debe seguir vigente.
        var tokenGuardado = await DbContext.RefreshTokens.SingleAsync();
        Assert.Null(tokenGuardado.FechaRevocacion);
    }
}
