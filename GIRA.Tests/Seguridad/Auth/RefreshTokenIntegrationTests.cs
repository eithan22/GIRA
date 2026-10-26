
using GIRA.Application;
using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;


namespace GIRA.Tests.Seguridad;

/// <summary>
/// Pruebas de integración de GI-96 con SQL Server.
/// </summary>
[Trait("Category", "Integration")]
public class RefreshTokenIntegrationTests
{
    [Fact]
    public void Configuracion_Pruebas_UsaBaseAislada()
    {
        // 1. Leer los User Secrets de GIRA.Tests.
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        // 2. Obtener la cadena de conexión.
        var conexion = configuracion
            .GetConnectionString("GiraGi96Test");

        // 3. Confirmar que existe.
        Assert.False(string.IsNullOrWhiteSpace(conexion));

        // 4. Comprobar el nombre de la base.
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseSqlServer(conexion)
            .Options;

        using var db = new GiraDbContext(opciones);

        Assert.Equal(
            "GIRA_GI96_TEST",
            db.Database.GetDbConnection().Database);
    }

    [Fact]
    public async Task Conexion_SQLServer_Funciona()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion
            .GetConnectionString("GiraGi96Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        // Protección: nunca conectar estas pruebas a GIRA_DB.
        Assert.Equal("GIRA_GI96_TEST", builder.InitialCatalog);

        // Nos conectamos a master porque la base de pruebas
        // todavía podría no existir.
        builder.InitialCatalog = "master";

        await using var db = new Microsoft.Data.SqlClient.SqlConnection(
            builder.ConnectionString);

        await db.OpenAsync();

        Assert.Equal(
            System.Data.ConnectionState.Open,
            db.State);
    }


    [Fact]
    public async Task BaseDePruebas_AplicaMigraciones()
    {
        // Obtener la configuración local de GIRA.Tests.
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion
            .GetConnectionString("GiraGi96Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        // Comprobar que no estamos utilizando GIRA_DB.
        var datosConexion =
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        Assert.Equal("GIRA_GI96_TEST", datosConexion.InitialCatalog);

        // Crear el contexto de Entity Framework Core.
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseSqlServer(conexion)
            .Options;

        await using var db = new GiraDbContext(opciones);

        // Aplicar las migraciones existentes.
        await db.Database.MigrateAsync();

        // Confirmar que se puede consultar la base.
        Assert.True(await db.Database.CanConnectAsync());

        // Comprobar que existe la tabla de refresh tokens.
        var cantidadTokens = await db.RefreshTokens.CountAsync();

        Assert.True(cantidadTokens >= 0);
    }

    [Fact]
    public async Task RefreshAsync_TokenValido_RotaYRevocaAnterior()
    {
        // Configuración de la base de pruebas.
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi96Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        var datosConexion =
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        // Seguridad: impedir que esta prueba modifique GIRA_DB.
        Assert.Equal("GIRA_GI96_TEST", datosConexion.InitialCatalog);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddAuthentication();

        // Configuración necesaria para los JWT.
        var configuracionServicios = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GiraDb"] = conexion,
                ["Jwt:Issuer"] = "GIRA.Tests",
                ["Jwt:Audience"] = "GIRA.Tests",
                ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
                ["Jwt:AccessTokenMinutos"] = "30",
                ["Jwt:RefreshTokenDias"] = "7"
            })
            .Build();

        services.AddApplication();
        services.AddInfrastructure(configuracionServicios);

        using var proveedor = services.BuildServiceProvider();
        using var scope = proveedor.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<Usuario>>();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var db = scope.ServiceProvider
            .GetRequiredService<GiraDbContext>();

        // Crear un usuario exclusivo para esta prueba.
        var correo = $"gi96-{Guid.NewGuid():N}@gira.local";

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Usuario Prueba GI96",
            Activo = true
        };

        var creacion = await userManager.CreateAsync(
            usuario, "Prueba-Segura123!");

        Assert.True(creacion.Succeeded);

        // Generar un refresh token reutilizando el servicio de Luis.
        var tokenService = scope.ServiceProvider
            .GetRequiredService<ITokenService>();

        var anterior = await tokenService.CrearRefreshTokenAsync(
            usuario.Id, "127.0.0.1");

        // Renovar la sesión.
        var resultado = await authService.RefreshAsync(
            anterior.Token, "127.0.0.1");

        Assert.True(resultado.IsSuccess);

        var nuevoToken = resultado.Value!.RefreshToken;

        Assert.NotEqual(anterior.Token, nuevoToken);

        // Comprobar los registros guardados en SQL Server.
        var hashAnterior = tokenService.CalcularHash(anterior.Token);
        var hashNuevo = tokenService.CalcularHash(nuevoToken);

        var registroAnterior = await db.RefreshTokens
            .AsNoTracking()
            .SingleAsync(t => t.TokenHash == hashAnterior);

        var registroNuevo = await db.RefreshTokens
            .AsNoTracking()
            .SingleAsync(t => t.TokenHash == hashNuevo);

        Assert.NotNull(registroAnterior.FechaRevocacion);
        Assert.Equal(registroNuevo.Id, registroAnterior.ReemplazadoPorId);
        Assert.Null(registroNuevo.FechaRevocacion);
    }

    [Fact]
    public async Task RefreshAsync_TokenReutilizado_RevocaTokensActivos()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi96Test");
        Assert.False(string.IsNullOrWhiteSpace(conexion));

        var datosConexion =
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        Assert.Equal("GIRA_GI96_TEST", datosConexion.InitialCatalog);

        var configuracionServicios = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GiraDb"] = conexion,
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
        services.AddInfrastructure(configuracionServicios);

        using var proveedor = services.BuildServiceProvider();
        using var scope = proveedor.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<Usuario>>();

        var tokenService = scope.ServiceProvider
            .GetRequiredService<ITokenService>();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var db = scope.ServiceProvider
            .GetRequiredService<GiraDbContext>();

        var correo = $"gi96-reuso-{Guid.NewGuid():N}@gira.local";

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Prueba Reutilizacion",
            Activo = true
        };

        var creacion = await userManager.CreateAsync(
            usuario, "Prueba-Segura123!");

        Assert.True(creacion.Succeeded);

        // Crear el token A.
        var tokenA = await tokenService.CrearRefreshTokenAsync(
            usuario.Id, "127.0.0.1");

        // Rotar A y obtener B.
        var primeraRenovacion = await authService.RefreshAsync(
            tokenA.Token, "127.0.0.1");

        Assert.True(primeraRenovacion.IsSuccess);

        var tokenB = primeraRenovacion.Value!.RefreshToken;

        // Intentar reutilizar A.
        var reutilizacion = await authService.RefreshAsync(
            tokenA.Token, "127.0.0.1");

        Assert.True(reutilizacion.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, reutilizacion.Type);

        // B también debe quedar revocado.
        var hashB = tokenService.CalcularHash(tokenB);

        var registroB = await db.RefreshTokens
            .AsNoTracking()
            .SingleAsync(t => t.TokenHash == hashB);

        Assert.NotNull(registroB.FechaRevocacion);
    }



    [Fact]
    public async Task RefreshAsync_DosSolicitudesSimultaneas_SoloUnaExitosa()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi96Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        // Crear servicios usando la base aislada.
        using var proveedor = CrearProveedorPruebas(conexion);

        using var scopeInicial = proveedor.CreateScope();

        var userManager = scopeInicial.ServiceProvider
            .GetRequiredService<UserManager<Usuario>>();

        var tokenService = scopeInicial.ServiceProvider
            .GetRequiredService<ITokenService>();

        // Crear un usuario exclusivo para esta prueba.
        var correo = $"gi96-concurrencia-{Guid.NewGuid():N}@gira.local";

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Prueba Concurrencia",
            Activo = true
        };

        var creacion = await userManager.CreateAsync(
            usuario, "Prueba-Segura123!");

        Assert.True(creacion.Succeeded);

        var token = await tokenService.CrearRefreshTokenAsync(
            usuario.Id, "127.0.0.1");

        // Cada solicitud utiliza su propio scope y DbContext.
        using var scope1 = proveedor.CreateScope();
        using var scope2 = proveedor.CreateScope();

        var servicio1 = scope1.ServiceProvider
            .GetRequiredService<IAuthService>();

        var servicio2 = scope2.ServiceProvider
            .GetRequiredService<IAuthService>();

        // Lanzar dos renovaciones del mismo token.
        var tarea1 = servicio1.RefreshAsync(
            token.Token, "127.0.0.1");

        var tarea2 = servicio2.RefreshAsync(
            token.Token, "127.0.0.1");

        var resultados = await Task.WhenAll(tarea1, tarea2);

        var exitos = resultados.Count(r => r.IsSuccess);

        Assert.Equal(1, exitos);
    }


    [Fact]
    public async Task LogoutAsync_DosVeces_DevuelveExito()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<RefreshTokenIntegrationTests>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi96Test");

        Assert.False(string.IsNullOrWhiteSpace(conexion));

        // CrearProveedorPruebas valida la base aislada.
        using var proveedor = CrearProveedorPruebas(conexion);

        using var scope = proveedor.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<Usuario>>();

        var tokenService = scope.ServiceProvider
            .GetRequiredService<ITokenService>();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var db = scope.ServiceProvider
            .GetRequiredService<GiraDbContext>();

        var correo = $"gi96-logout-{Guid.NewGuid():N}@gira.local";

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Prueba Logout",
            Activo = true
        };

        var creacion = await userManager.CreateAsync(
            usuario, "Prueba-Segura123!");

        Assert.True(creacion.Succeeded);

        var token = await tokenService.CrearRefreshTokenAsync(
            usuario.Id, "127.0.0.1");

        // Primer logout.
        var primero = await authService.LogoutAsync(
            usuario.Id, token.Token);

        Assert.True(primero.IsSuccess);

        // Segundo logout con el mismo token.
        var segundo = await authService.LogoutAsync(
            usuario.Id, token.Token);

        Assert.True(segundo.IsSuccess);

        // Comprobar que continúa revocado en SQL Server.
        var hash = tokenService.CalcularHash(token.Token);

        var registro = await db.RefreshTokens
            .AsNoTracking()
            .SingleAsync(t => t.TokenHash == hash);

        Assert.NotNull(registro.FechaRevocacion);
    }

    private static ServiceProvider CrearProveedorPruebas(
        string conexion)
    {
        var datosConexion =
    new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        if (!string.Equals(
            datosConexion.InitialCatalog,
            "GIRA_GI96_TEST",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Las pruebas GI-96 solo pueden utilizar GIRA_GI96_TEST.");
        }
        var configuracion = new ConfigurationBuilder()
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

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddAuthentication();
        services.AddApplication();
        services.AddInfrastructure(configuracion);

        return services.BuildServiceProvider();
    }


}
