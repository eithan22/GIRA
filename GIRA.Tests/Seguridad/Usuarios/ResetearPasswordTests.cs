using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Usuarios;
using GIRA.Application.Validations.Seguridad.Usuarios;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad.Usuarios;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Seguridad.Usuarios;

/// <summary>
/// GI-221: Pruebas del reseteo de contraseña por un Administrador (GI-218).
/// </summary>
public class ResetearPasswordTests
{
    private const string PasswordInicial = "Temporal123!";
    private const string PasswordEndpoint = "Clave-Segura1";

    // Crea el servicio con base de datos en memoria, aislada por prueba. Las opciones de
    // contraseña son las mismas de AddSeguridad() para poder comprobar RN-SEG-02.
    private static (UsuarioService servicio, GiraDbContext db, UserManager<Usuario> um) Crear()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GiraDbContext>(o => o
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // El proveedor InMemory no soporta transacciones; ResetearPasswordAsync abre una a
            // propósito (ver el comentario en el servicio), así que se ignora la advertencia.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<Usuario>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireUppercase = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = true;
            o.User.RequireUniqueEmail = true;
        })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<GiraDbContext>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<GiraDbContext>();
        var um = provider.GetRequiredService<UserManager<Usuario>>();

        var servicio = new UsuarioService(
            um, db,
            new CrearUsuarioRequestValidator(),
            new ActualizarUsuarioRequestValidator(),
            new UsuarioFiltroRequestValidator(),
            new AsignarRolesRequestValidator());

        return (servicio, db, um);
    }

    private static async Task<Usuario> CrearUsuarioAsync(UserManager<Usuario> um, string email = "mesero@gira.com")
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = "Mesero de Prueba",
            Activo = true
        };

        var creado = await um.CreateAsync(usuario, PasswordInicial);
        Assert.True(creado.Succeeded);
        return usuario;
    }

    private static RefreshToken Token(Guid usuarioId, DateTime? revocado = null) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        TokenHash = Guid.NewGuid().ToString("N"),
        FechaCreacion = DateTime.UtcNow,
        FechaExpiracion = DateTime.UtcNow.AddDays(7),
        IpCreacion = "127.0.0.1",
        FechaRevocacion = revocado
    };

    // RN-SEG-02: la contraseña generada cumple largo, mayúscula, minúscula, dígito y símbolo.
    [Fact]
    public async Task ResetearPassword_UsuarioExistente_DevuelvePasswordQueCumpleLaPolitica()
    {
        var (servicio, _, um) = Crear();
        var usuario = await CrearUsuarioAsync(um);

        var r = await servicio.ResetearPasswordAsync(usuario.Id);

        Assert.True(r.IsSuccess);
        var password = r.Value!.PasswordTemporal;
        Assert.True(password.Length >= 8);
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        Assert.Equal(usuario.Id, r.Value.Usuario.Id);
    }

    // La contraseña nueva sirve, la anterior no, y el usuario queda obligado a cambiarla.
    [Fact]
    public async Task ResetearPassword_UsuarioExistente_CambiaLaPasswordYMarcaDebeCambiarPassword()
    {
        var (servicio, _, um) = Crear();
        var usuario = await CrearUsuarioAsync(um);

        var r = await servicio.ResetearPasswordAsync(usuario.Id);

        var actualizado = await um.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(actualizado);
        Assert.True(await um.CheckPasswordAsync(actualizado!, r.Value!.PasswordTemporal));
        Assert.False(await um.CheckPasswordAsync(actualizado!, PasswordInicial));
        Assert.True(actualizado!.DebeCambiarPassword);
    }

    // Las sesiones abiertas dejan de servir; un token ya revocado conserva su fecha.
    [Fact]
    public async Task ResetearPassword_RevocaLosRefreshTokensActivos()
    {
        var (servicio, db, um) = Crear();
        var usuario = await CrearUsuarioAsync(um);
        var otro = await CrearUsuarioAsync(um, "otro@gira.com");
        var revocadoAntes = DateTime.UtcNow.AddDays(-1);

        db.RefreshTokens.AddRange(
            Token(usuario.Id),
            Token(usuario.Id),
            Token(usuario.Id, revocadoAntes),
            Token(otro.Id));
        await db.SaveChangesAsync();

        var r = await servicio.ResetearPasswordAsync(usuario.Id);

        Assert.True(r.IsSuccess);
        var tokens = await db.RefreshTokens.AsNoTracking().ToListAsync();
        Assert.All(tokens.Where(t => t.UsuarioId == usuario.Id), t => Assert.NotNull(t.FechaRevocacion));
        Assert.Equal(revocadoAntes, tokens.Single(t => t.FechaRevocacion == revocadoAntes).FechaRevocacion);
        // El reseteo de un usuario no toca las sesiones de otro.
        Assert.Null(tokens.Single(t => t.UsuarioId == otro.Id).FechaRevocacion);
    }

    // La solicitud de recuperación queda atendida: sale de la cola del Administrador.
    [Fact]
    public async Task ResetearPassword_ConSolicitudPendiente_LaBorra()
    {
        var (servicio, db, um) = Crear();
        var usuario = await CrearUsuarioAsync(um);
        var otro = await CrearUsuarioAsync(um, "otro@gira.com");

        db.SolicitudesRecuperacionPassword.AddRange(
            new SolicitudRecuperacionPassword { Id = Guid.NewGuid(), UsuarioId = usuario.Id, FechaSolicitud = DateTime.UtcNow },
            new SolicitudRecuperacionPassword { Id = Guid.NewGuid(), UsuarioId = otro.Id, FechaSolicitud = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var r = await servicio.ResetearPasswordAsync(usuario.Id);

        Assert.True(r.IsSuccess);
        var pendientes = await db.SolicitudesRecuperacionPassword.AsNoTracking().Select(s => s.UsuarioId).ToListAsync();
        Assert.Equal(new[] { otro.Id }, pendientes);
    }

    // Resetear sin solicitud previa es válido: el Administrador puede hacerlo por su cuenta.
    [Fact]
    public async Task ResetearPassword_SinSolicitudPendiente_FuncionaIgual()
    {
        var (servicio, db, um) = Crear();
        var usuario = await CrearUsuarioAsync(um);

        var r = await servicio.ResetearPasswordAsync(usuario.Id);

        Assert.True(r.IsSuccess);
        Assert.Empty(await db.SolicitudesRecuperacionPassword.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ResetearPassword_UsuarioInexistente_DevuelveNotFound()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.ResetearPasswordAsync(Guid.NewGuid());

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Type);
    }

    // El endpoint está detrás de la política GestionarUsuarios: solo Administrador.
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Roles.Gerente, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Supervisor, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Administrador, HttpStatusCode.OK)]
    public async Task ResetearPasswordEndpoint_SegunElRol_DevuelveElEstadoEsperado(string? rol, HttpStatusCode esperado)
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();
        var objetivo = await factory.CrearUsuarioAsync("objetivo@gira.local", PasswordEndpoint, true, Roles.Mesero);

        if (rol is not null)
        {
            var correo = $"quien-{Guid.NewGuid():N}@gira.local";
            await factory.CrearUsuarioAsync(correo, PasswordEndpoint, true, rol);
            cliente.UsarToken(await cliente.ObtenerTokenAsync(correo, PasswordEndpoint));
        }

        var respuesta = await cliente.PostAsync($"/api/usuarios/{objetivo}/resetear-password", content: null);

        Assert.Equal(esperado, respuesta.StatusCode);

        if (esperado == HttpStatusCode.OK)
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<PasswordReseteadaDto>();
            Assert.False(string.IsNullOrWhiteSpace(cuerpo!.PasswordTemporal));
            Assert.Equal(objetivo, cuerpo.Usuario.Id);
        }
    }

    // Id inexistente -> 404 a través del pipeline HTTP.
    [Fact]
    public async Task ResetearPasswordEndpoint_IdInexistente_Devuelve404()
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();
        await factory.CrearUsuarioAsync("admin@gira.local", PasswordEndpoint, true, Roles.Administrador);
        cliente.UsarToken(await cliente.ObtenerTokenAsync("admin@gira.local", PasswordEndpoint));

        var respuesta = await cliente.PostAsync($"/api/usuarios/{Guid.NewGuid()}/resetear-password", content: null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
