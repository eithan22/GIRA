using GIRA.Application;
using GIRA.Application.Interfaces.Services.Seguridad.Accesos;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Roles;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Services.Seguridad.Accesos;
using GIRA.Infrastructure.Services.Seguridad.Auth;
using GIRA.Infrastructure.Services.Seguridad.Roles;
using GIRA.Infrastructure.Services.Seguridad.Usuarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Seguridad;

/// <summary>
/// Verifica los registros de DI de AddSeguridad() leyendo los ServiceDescriptor de la
/// ServiceCollection, sin construir el proveedor ni tocar una base de datos real.
/// </summary>
public class SeguridadServiceExtensionsTests
{
    private static ServiceCollection Registrar()
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
        services.AddApplication();
        services.AddInfrastructure(configuracion);
        return services;
    }

    private static ServiceDescriptor? Buscar(ServiceCollection services, Type tipoServicio) =>
        services.LastOrDefault(d => d.ServiceType == tipoServicio);

    /// <summary>Los 7 servicios de Seguridad que AddSeguridad() debe registrar como Scoped.</summary>
    public static IEnumerable<object[]> RegistrosEsperados()
    {
        yield return new object[] { typeof(ITokenService), typeof(TokenService) };
        yield return new object[] { typeof(IRotadorRefreshToken), typeof(RotadorRefreshToken) };
        yield return new object[] { typeof(IAuthService), typeof(AuthService) };
        yield return new object[] { typeof(IRegistroAccesoService), typeof(RegistroAccesoService) };
        yield return new object[] { typeof(IHistorialAccesosService), typeof(HistorialAccesosService) };
        yield return new object[] { typeof(IUsuarioService), typeof(UsuarioService) };
        yield return new object[] { typeof(IRolService), typeof(RolService) };
    }

    [Theory]
    [MemberData(nameof(RegistrosEsperados))]
    public void AddSeguridad_RegistraServicio_ConImplementacionYVidaScoped(Type tipoServicio, Type implementacionEsperada)
    {
        var services = Registrar();

        var descriptor = Buscar(services, tipoServicio);

        Assert.True(descriptor is not null, $"No se encontró un registro para {tipoServicio.Name}.");
        Assert.Equal(implementacionEsperada, descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddSeguridad_RegistraJwtSettings_ComoSingleton()
    {
        var services = Registrar();

        var descriptor = Buscar(services, typeof(JwtSettings));

        Assert.True(descriptor is not null, "No se encontró un registro para JwtSettings.");
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }
}
