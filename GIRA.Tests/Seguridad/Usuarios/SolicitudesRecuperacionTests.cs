using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Usuarios;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
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
/// GI-221: Pruebas de la cola de solicitudes de recuperación de contraseña (GI-219):
/// listarlas y descartarlas sin tocar la contraseña.
/// </summary>
public class SolicitudesRecuperacionTests
{
    private const string PasswordInicial = "Temporal123!";
    private const string PasswordEndpoint = "Clave-Segura1";

    // Mismo montaje que ResetearPasswordTests: base en memoria aislada por prueba.
    private static (UsuarioService servicio, GiraDbContext db, UserManager<Usuario> um) Crear()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GiraDbContext>(o => o
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private static async Task<Usuario> CrearUsuarioAsync(UserManager<Usuario> um, string email, string nombre)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = nombre,
            Activo = true
        };

        var creado = await um.CreateAsync(usuario, PasswordInicial);
        Assert.True(creado.Succeeded);
        return usuario;
    }

    private static async Task SolicitarAsync(GiraDbContext db, Guid usuarioId, DateTime fecha)
    {
        db.SolicitudesRecuperacionPassword.Add(new SolicitudRecuperacionPassword
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            FechaSolicitud = fecha
        });
        await db.SaveChangesAsync();
    }

    // La cola se atiende por orden de llegada: la más antigua primero.
    [Fact]
    public async Task ListarSolicitudes_ConVarias_LasDevuelveDeLaMasAntiguaALaMasReciente()
    {
        var (servicio, db, um) = Crear();
        var primero = await CrearUsuarioAsync(um, "primero@gira.com", "Primero Pérez");
        var segundo = await CrearUsuarioAsync(um, "segundo@gira.com", "Segundo Pérez");
        var ahora = DateTime.UtcNow;

        await SolicitarAsync(db, segundo.Id, ahora);
        await SolicitarAsync(db, primero.Id, ahora.AddHours(-3));

        var r = await servicio.ListarSolicitudesRecuperacionAsync();

        Assert.True(r.IsSuccess);
        Assert.Equal(new[] { primero.Id, segundo.Id }, r.Value!.Select(s => s.UsuarioId));
        var primera = r.Value![0];
        Assert.Equal("primero@gira.com", primera.Email);
        Assert.Equal("Primero Pérez", primera.NombreCompleto);
        Assert.Equal(DateTimeKind.Utc, primera.FechaSolicitud.Kind);
    }

    [Fact]
    public async Task ListarSolicitudes_SinSolicitudes_DevuelveListaVacia()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.ListarSolicitudesRecuperacionAsync();

        Assert.True(r.IsSuccess);
        Assert.Empty(r.Value!);
    }

    // Descartar saca la solicitud de la cola y no cambia la contraseña del usuario.
    [Fact]
    public async Task DescartarSolicitud_Existente_LaBorraYNoCambiaLaPassword()
    {
        var (servicio, db, um) = Crear();
        var usuario = await CrearUsuarioAsync(um, "olvidadizo@gira.com", "Olvidadizo Pérez");
        await SolicitarAsync(db, usuario.Id, DateTime.UtcNow);

        var r = await servicio.DescartarSolicitudRecuperacionAsync(usuario.Id);

        Assert.True(r.IsSuccess);
        Assert.Empty(await db.SolicitudesRecuperacionPassword.AsNoTracking().ToListAsync());

        var actualizado = await um.FindByIdAsync(usuario.Id.ToString());
        Assert.True(await um.CheckPasswordAsync(actualizado!, PasswordInicial));
        Assert.False(actualizado!.DebeCambiarPassword);
    }

    // Sin solicitud pendiente -> 404: alguien más la atendió antes.
    [Fact]
    public async Task DescartarSolicitud_Inexistente_DevuelveNotFound()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.DescartarSolicitudRecuperacionAsync(Guid.NewGuid());

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Type);
    }

    // GET /api/usuarios/solicitudes-recuperacion está detrás de GestionarUsuarios.
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Roles.Gerente, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Mesero, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Administrador, HttpStatusCode.OK)]
    public async Task ListarSolicitudesEndpoint_SegunElRol_DevuelveElEstadoEsperado(string? rol, HttpStatusCode esperado)
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();

        if (rol is not null)
        {
            var correo = $"quien-{Guid.NewGuid():N}@gira.local";
            await factory.CrearUsuarioAsync(correo, PasswordEndpoint, true, rol);
            cliente.UsarToken(await cliente.ObtenerTokenAsync(correo, PasswordEndpoint));
        }

        var respuesta = await cliente.GetAsync("/api/usuarios/solicitudes-recuperacion");

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    // DELETE /api/usuarios/solicitudes-recuperacion/{usuarioId} también es solo de Administrador.
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Roles.Gerente, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Cocina, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Administrador, HttpStatusCode.NoContent)]
    public async Task DescartarSolicitudEndpoint_SegunElRol_DevuelveElEstadoEsperado(string? rol, HttpStatusCode esperado)
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();
        var objetivo = await factory.CrearUsuarioAsync("objetivo@gira.local", PasswordEndpoint, true, Roles.Mesero);

        // La solicitud la crea el propio endpoint anónimo de GI-215.
        var solicitud = await cliente.PostAsJsonAsync(
            "/api/auth/olvide-password",
            new GIRA.Application.DTOs.Seguridad.Auth.OlvidePasswordRequest { Email = "objetivo@gira.local" });
        Assert.Equal(HttpStatusCode.OK, solicitud.StatusCode);

        if (rol is not null)
        {
            var correo = $"quien-{Guid.NewGuid():N}@gira.local";
            await factory.CrearUsuarioAsync(correo, PasswordEndpoint, true, rol);
            cliente.UsarToken(await cliente.ObtenerTokenAsync(correo, PasswordEndpoint));
        }

        var respuesta = await cliente.DeleteAsync($"/api/usuarios/solicitudes-recuperacion/{objetivo}");

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    // El listado del Administrador ve la solicitud que dejó el endpoint anónimo (flujo completo
    // GI-212 -> GI-213) y deja de verla después de resetear la contraseña.
    [Fact]
    public async Task FlujoCompleto_SolicitudListadaYAtendidaConElReseteo()
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();
        var objetivo = await factory.CrearUsuarioAsync("objetivo@gira.local", PasswordEndpoint, true, Roles.Mesero);
        await factory.CrearUsuarioAsync("admin@gira.local", PasswordEndpoint, true, Roles.Administrador);

        await cliente.PostAsJsonAsync(
            "/api/auth/olvide-password",
            new GIRA.Application.DTOs.Seguridad.Auth.OlvidePasswordRequest { Email = "objetivo@gira.local" });

        cliente.UsarToken(await cliente.ObtenerTokenAsync("admin@gira.local", PasswordEndpoint));

        var pendientes = await cliente.GetFromJsonAsync<List<SolicitudRecuperacionDto>>(
            "/api/usuarios/solicitudes-recuperacion");

        Assert.Equal(new[] { objetivo }, pendientes!.Select(s => s.UsuarioId));

        var reseteo = await cliente.PostAsync($"/api/usuarios/{objetivo}/resetear-password", content: null);
        Assert.Equal(HttpStatusCode.OK, reseteo.StatusCode);

        var despues = await cliente.GetFromJsonAsync<List<SolicitudRecuperacionDto>>(
            "/api/usuarios/solicitudes-recuperacion");

        Assert.Empty(despues!);
    }

    // El servicio registrado en DI es el real: la cola se ve igual resolviéndolo del contenedor.
    [Fact]
    public async Task ServicioDeDi_ListarSolicitudes_FuncionaSobreLaBaseDeLaApi()
    {
        await using var factory = new GiraApiFactory();
        using var cliente = factory.CreateClient();
        await factory.CrearUsuarioAsync("objetivo@gira.local", PasswordEndpoint, true, Roles.Mesero);

        await cliente.PostAsJsonAsync(
            "/api/auth/olvide-password",
            new GIRA.Application.DTOs.Seguridad.Auth.OlvidePasswordRequest { Email = "objetivo@gira.local" });

        using var scope = factory.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();

        var r = await servicio.ListarSolicitudesRecuperacionAsync();

        Assert.True(r.IsSuccess);
        Assert.Single(r.Value!);
    }
}
