using GIRA.Application;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GIRA.Tests.Seguridad.Auth;

/// <summary>
/// Pruebas del login (GI-95) con la configuración real de Identity de AddInfrastructure
/// (bloqueo de 5 intentos por 15 minutos) sobre una base en memoria aislada por prueba.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private const string Correo = "ana.perez@gira.local";
    private const string Password = "Clave-Segura1";
    private const string Ip = "127.0.0.1";

    private readonly ServiceProvider _proveedor;
    private readonly IServiceScope _scope;

    public AuthServiceTests()
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

        // Reemplaza SQL Server por una base en memoria propia de cada prueba.
        services.RemoveAll<DbContextOptions<GiraDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<GiraDbContext>>();
        var nombreBase = Guid.NewGuid().ToString();
        // El proveedor InMemory no soporta transacciones; CambiarPasswordAsync abre una a
        // propósito (ver el comentario en AuthService), así que se ignora la advertencia.
        services.AddDbContext<GiraDbContext>(options => options
            .UseInMemoryDatabase(nombreBase)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        _proveedor = services.BuildServiceProvider();
        _scope = _proveedor.CreateScope();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _proveedor.Dispose();
    }

    private IAuthService Servicio => _scope.ServiceProvider.GetRequiredService<IAuthService>();
    private UserManager<Usuario> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    private GiraDbContext DbContext => _scope.ServiceProvider.GetRequiredService<GiraDbContext>();

    private async Task<Usuario> CrearUsuarioAsync(bool activo = true, params string[] roles)
    {
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
        foreach (var rol in Roles.Todos)
        {
            await roleManager.CreateAsync(new Rol { Name = rol });
        }

        var usuario = new Usuario { UserName = Correo, Email = Correo, NombreCompleto = "Ana Pérez", Activo = activo };
        Assert.True((await UserManager.CreateAsync(usuario, Password)).Succeeded);
        Assert.True((await UserManager.AddToRolesAsync(usuario, roles.Length > 0 ? roles : [Roles.Mesero])).Succeeded);
        return usuario;
    }

    private Task<OperationResult<LoginResponse>> LoginAsync(string correo, string password)
    {
        return Servicio.LoginAsync(new LoginRequest { Email = correo, Password = password }, Ip);
    }

    // Credenciales válidas -> access token de 30 min con roles, refresh token de 7 días y datos básicos.
    [Fact]
    public async Task LoginAsync_CredencialesValidas_DevuelveTokensYDatosDelUsuario()
    {
        var usuario = await CrearUsuarioAsync(true, Roles.Gerente, Roles.Supervisor);

        var resultado = await LoginAsync(Correo, Password);

        Assert.True(resultado.IsSuccess);
        var respuesta = resultado.Value!;
        Assert.Equal(usuario.Id, respuesta.Usuario.Id);
        Assert.Equal(Correo, respuesta.Usuario.Email);
        Assert.Equal("Ana Pérez", respuesta.Usuario.NombreCompleto);
        Assert.Equal(new[] { Roles.Gerente, Roles.Supervisor }, respuesta.Usuario.Roles.Order());

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(respuesta.AccessToken);
        Assert.Equal(usuario.Id.ToString(), jwt.Subject);
        Assert.Equal(new[] { Roles.Gerente, Roles.Supervisor },
            jwt.Claims.Where(c => c.Type == TiposClaim.Rol).Select(c => c.Value).Order());
        Assert.Equal(TimeSpan.FromMinutes(30), jwt.ValidTo - jwt.IssuedAt);

        Assert.InRange(respuesta.RefreshTokenExpiraEn - DateTime.UtcNow,
            TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(7));
    }

    // El refresh token se guarda como hash SHA-256 (64 caracteres hex), nunca en claro.
    [Fact]
    public async Task LoginAsync_CredencialesValidas_GuardaSoloElHashDelRefreshToken()
    {
        var usuario = await CrearUsuarioAsync();

        var resultado = await LoginAsync(Correo, Password);

        var guardado = Assert.Single(await DbContext.RefreshTokens.ToListAsync());
        var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();
        Assert.Equal(usuario.Id, guardado.UsuarioId);
        Assert.Equal(tokenService.CalcularHash(resultado.Value!.RefreshToken), guardado.TokenHash);
        Assert.NotEqual(resultado.Value.RefreshToken, guardado.TokenHash);
        Assert.Equal(64, guardado.TokenHash.Length);
        Assert.Equal(Ip, guardado.IpCreacion);
    }

    // Un login exitoso actualiza UltimoAcceso.
    [Fact]
    public async Task LoginAsync_CredencialesValidas_ActualizaUltimoAcceso()
    {
        await CrearUsuarioAsync();

        await LoginAsync(Correo, Password);

        var usuario = await UserManager.FindByEmailAsync(Correo);
        Assert.NotNull(usuario!.UltimoAcceso);
    }

    // Contraseña incorrecta -> 401 con el mensaje genérico.
    [Fact]
    public async Task LoginAsync_PasswordIncorrecta_DevuelveNoAutorizadoConMensajeGenerico()
    {
        await CrearUsuarioAsync();

        var resultado = await LoginAsync(Correo, "Otra-Clave1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, resultado.Type);
        Assert.Equal(AuthService.MensajeCredencialesInvalidas, resultado.Error);
    }

    // Correo inexistente -> el mismo error que una contraseña incorrecta.
    [Fact]
    public async Task LoginAsync_CorreoInexistente_DevuelveElMismoErrorQuePasswordIncorrecta()
    {
        await CrearUsuarioAsync();

        var correoInexistente = await LoginAsync("nadie@gira.local", Password);
        var passwordIncorrecta = await LoginAsync(Correo, "Otra-Clave1");

        Assert.Equal(passwordIncorrecta.Type, correoInexistente.Type);
        Assert.Equal(passwordIncorrecta.Error, correoInexistente.Error);
    }

    // 5 fallos consecutivos bloquean 15 minutos aunque luego la contraseña sea correcta.
    [Fact]
    public async Task LoginAsync_CincoFallosConsecutivos_BloqueaAunqueLuegoLaPasswordSeaCorrecta()
    {
        await CrearUsuarioAsync();

        for (var intento = 0; intento < 5; intento++)
        {
            await LoginAsync(Correo, "Otra-Clave1");
        }

        var resultado = await LoginAsync(Correo, Password);

        Assert.Equal(ErrorType.Unauthorized, resultado.Type);
        Assert.Equal(AuthService.MensajeCredencialesInvalidas, resultado.Error);
        var usuario = await UserManager.FindByEmailAsync(Correo);
        Assert.True(await UserManager.IsLockedOutAsync(usuario!));
        Assert.InRange(usuario!.LockoutEnd!.Value - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    // Los fallos deben ser consecutivos: un login exitoso reinicia el contador.
    [Fact]
    public async Task LoginAsync_LoginExitosoEntreFallos_NoBloqueaLaCuenta()
    {
        await CrearUsuarioAsync();

        for (var intento = 0; intento < 4; intento++)
        {
            await LoginAsync(Correo, "Otra-Clave1");
        }
        await LoginAsync(Correo, Password);
        for (var intento = 0; intento < 4; intento++)
        {
            await LoginAsync(Correo, "Otra-Clave1");
        }

        var resultado = await LoginAsync(Correo, Password);

        Assert.True(resultado.IsSuccess);
    }

    // Usuario desactivado -> no puede iniciar sesión aunque la contraseña sea correcta.
    [Fact]
    public async Task LoginAsync_UsuarioDesactivado_DevuelveNoAutorizado()
    {
        await CrearUsuarioAsync(activo: false);

        var resultado = await LoginAsync(Correo, Password);

        Assert.Equal(ErrorType.Unauthorized, resultado.Type);
        Assert.Equal(AuthService.MensajeCredencialesInvalidas, resultado.Error);
        Assert.Empty(await DbContext.RefreshTokens.ToListAsync());
    }

    // Correo con formato inválido -> 400 antes de consultar la base.
    [Theory]
    [InlineData("", Password)]
    [InlineData("no-es-un-correo", Password)]
    [InlineData(Correo, "")]
    public async Task LoginAsync_DatosInvalidos_DevuelveErrorDeValidacion(string correo, string password)
    {
        var resultado = await LoginAsync(correo, password);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }


    // GI-146: Una contraseña actual incorrecta es un error de validación (400).
    [Fact]
    public async Task CambiarPassword_PasswordActualIncorrecta_DevuelveValidation()
    {
        var usuario = await CrearUsuarioAsync();

        var request = new CambiarPasswordRequest
        {
            PasswordActual = "Incorrecta123!",
            PasswordNueva = "NuevaSegura123!"
        };

        var resultado = await Servicio.CambiarPasswordAsync(
            usuario.Id, request);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.True(await UserManager.CheckPasswordAsync(usuario, Password));
    }

    // GI-146: No se permite reutilizar la contraseña.
    [Fact]
    public async Task CambiarPassword_PasswordNuevaIgual_DevuelveValidation()
    {
        var usuario = await CrearUsuarioAsync();

        var request = new CambiarPasswordRequest
        {
            PasswordActual = Password,
            PasswordNueva = Password
        };

        var resultado = await Servicio.CambiarPasswordAsync(
            usuario.Id, request);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    // GI-146: Validar la política de contraseñas.
    [Theory]
    [InlineData("Corta1!")]
    [InlineData("sinmayuscula123!")]
    [InlineData("SINMINUSCULA123!")]
    [InlineData("SinNumeros!")]
    [InlineData("SinSimbolo123")]
    public async Task CambiarPassword_PoliticaInvalida_DevuelveValidation(
        string nuevaPassword)
    {
        var usuario = await CrearUsuarioAsync();

        var request = new CambiarPasswordRequest
        {
            PasswordActual = Password,
            PasswordNueva = nuevaPassword
        };

        var resultado = await Servicio.CambiarPasswordAsync(
            usuario.Id, request);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    // GI-146 / 4.5: Identity es la única fuente de la política; su mensaje debe salir en español.
    [Fact]
    public async Task CambiarPassword_SinOchoCaracteres_MensajeEnEspanol()
    {
        var usuario = await CrearUsuarioAsync();
        var request = new CambiarPasswordRequest { PasswordActual = Password, PasswordNueva = "Corta1!" };

        var resultado = await Servicio.CambiarPasswordAsync(usuario.Id, request);

        Assert.Contains("al menos 8 caracteres", resultado.Error);
    }

    [Fact]
    public async Task CambiarPassword_SinMayuscula_MensajeEnEspanol()
    {
        var usuario = await CrearUsuarioAsync();
        var request = new CambiarPasswordRequest { PasswordActual = Password, PasswordNueva = "sinmayuscula123!" };

        var resultado = await Servicio.CambiarPasswordAsync(usuario.Id, request);

        Assert.Contains("mayúscula", resultado.Error);
    }

    // Varias reglas inválidas a la vez: Identity concatena un Description por regla, en el
    // orden en que las valida (longitud, símbolo, número, minúscula, mayúscula).
    [Fact]
    public async Task CambiarPassword_VariasReglasInvalidas_ConcatenaUnMensajePorRegla()
    {
        var usuario = await CrearUsuarioAsync();
        var request = new CambiarPasswordRequest { PasswordActual = Password, PasswordNueva = "abc" };

        var resultado = await Servicio.CambiarPasswordAsync(usuario.Id, request);

        Assert.Equal(
            "La contraseña debe tener al menos 8 caracteres. " +
            "La contraseña debe tener al menos un símbolo. " +
            "La contraseña debe tener al menos un número. " +
            "La contraseña debe tener al menos una mayúscula.",
            resultado.Error);
    }

}
