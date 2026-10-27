using GIRA.Application;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Services.Seguridad.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GIRA.Tests.Seguridad.Usuarios;

/// <summary>
/// Verifica la contraseña temporal contra la política de Identity configurada en
/// AddSeguridad (leída del contenedor, no repetida a mano), sin tocar una base real.
/// </summary>
public class GeneradorPasswordTemporalTests
{
    /// <summary>Arma el contenedor igual que AddSeguridad y devuelve la política de contraseña configurada.</summary>
    private static PasswordOptions LeerPoliticaDeContrasena()
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

        using var proveedor = services.BuildServiceProvider();
        return proveedor.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;
    }

    [Fact]
    public void Generar_DevuelveContrasenaDe12CaracteresQueCumpleLaPoliticaDeAddSeguridad()
    {
        var politica = LeerPoliticaDeContrasena();

        var password = GeneradorPasswordTemporal.Generar();

        Assert.Equal(12, password.Length);
        Assert.True(password.Length >= politica.RequiredLength,
            $"La contraseña generada ({password.Length} caracteres) no cumple RequiredLength ({politica.RequiredLength}).");

        if (politica.RequireUppercase)
            Assert.Contains(password, char.IsUpper);

        if (politica.RequireLowercase)
            Assert.Contains(password, char.IsLower);

        if (politica.RequireDigit)
            Assert.Contains(password, char.IsDigit);

        if (politica.RequireNonAlphanumeric)
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Generar_DosLlamadasSeguidas_DevuelveValoresDistintos()
    {
        var primera = GeneradorPasswordTemporal.Generar();
        var segunda = GeneradorPasswordTemporal.Generar();

        Assert.NotEqual(primera, segunda);
    }
}
