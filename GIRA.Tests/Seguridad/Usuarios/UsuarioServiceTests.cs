using GIRA.Application.DTOs.Seguridad.Usuarios;
using GIRA.Application.Common;
using GIRA.Application.Validations.Seguridad.Usuarios;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GIRA.Tests.Seguridad.Usuarios;

public class UsuarioServiceTests
{
    // Crea un servicio con base de datos en memoria, aislada por prueba.
    private static (UsuarioService servicio, GiraDbContext db, UserManager<Usuario> um) Crear()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GiraDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
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

    private static async Task<Usuario> CrearAdminAsync(UserManager<Usuario> um, GiraDbContext db, string email)
    {
        if (!await db.Roles.AnyAsync(r => r.Name == Roles.Administrador))
        {
            db.Roles.Add(new Rol
            {
                Id = Guid.NewGuid(),
                Name = Roles.Administrador,
                NormalizedName = Roles.Administrador.ToUpperInvariant()
            });
            await db.SaveChangesAsync();
        }

        var admin = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = "Admin",
            Activo = true
        };
        await um.CreateAsync(admin, "Admin123!*");
        await um.AddToRoleAsync(admin, Roles.Administrador);
        return admin;
    }

    [Fact]
    public async Task Crear_ConDatosValidos_DevuelvePasswordTemporal()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.CrearAsync(
            new CrearUsuarioRequest("nuevo@gira.com", "Nuevo Usuario", null));

        Assert.True(r.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(r.Value!.PasswordTemporal));
        Assert.Equal("nuevo@gira.com", r.Value.Usuario.Email);
    }

    [Fact]
    public async Task Crear_ConCorreoDuplicado_DevuelveConflicto()
    {
        var (servicio, _, _) = Crear();
        var request = new CrearUsuarioRequest("dup@gira.com", "Dup", null);

        await servicio.CrearAsync(request);
        var r = await servicio.CrearAsync(request);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.Conflict, r.Type);
    }

    [Fact]
    public async Task Crear_ConCorreoInvalido_DevuelveValidacion()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.CrearAsync(
            new CrearUsuarioRequest("no-es-correo", "", null));

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.Validation, r.Type);
    }

    [Fact]
    public async Task Obtener_UsuarioInexistente_DevuelveNotFound()
    {
        var (servicio, _, _) = Crear();

        var r = await servicio.ObtenerAsync(Guid.NewGuid());

        Assert.Equal(ErrorType.NotFound, r.Type);
    }

    [Fact]
    public async Task Desactivar_RevocaLosRefreshTokensVigentes()
    {
        var (servicio, db, _) = Crear();
        var creado = await servicio.CrearAsync(
            new CrearUsuarioRequest("token@gira.com", "Token", null));
        var id = creado.Value!.Usuario.Id;

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = id,
            TokenHash = "hash",
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddDays(7),
            IpCreacion = "127.0.0.1"
        });
        await db.SaveChangesAsync();

        var r = await servicio.CambiarEstadoAsync(id, false);

        Assert.True(r.IsSuccess);
        var token = await db.RefreshTokens.SingleAsync(t => t.UsuarioId == id);
        Assert.NotNull(token.FechaRevocacion);
    }

    [Fact]
    public async Task Desactivar_UltimoAdministrador_DevuelveConflicto()
    {
        var (servicio, db, um) = Crear();
        var admin = await CrearAdminAsync(um, db, "admin@gira.com");

        var r = await servicio.CambiarEstadoAsync(admin.Id, false);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.Conflict, r.Type);
    }

    [Fact]
    public async Task Desactivar_AdministradorConOtroActivo_Funciona()
    {
        var (servicio, db, um) = Crear();
        var admin1 = await CrearAdminAsync(um, db, "a1@gira.com");
        await CrearAdminAsync(um, db, "a2@gira.com");

        var r = await servicio.CambiarEstadoAsync(admin1.Id, false);

        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task Crear_ConEmpleadoYaVinculado_DevuelveConflicto()
    {
        var (servicio, _, _) = Crear();
        var empleadoId = Guid.NewGuid();

        await servicio.CrearAsync(new CrearUsuarioRequest("uno@gira.com", "Uno", empleadoId));
        var r = await servicio.CrearAsync(new CrearUsuarioRequest("dos@gira.com", "Dos", empleadoId));

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.Conflict, r.Type);
        Assert.Contains("empleado", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Actualizar_ConEmpleadoDeOtroUsuario_DevuelveConflicto()
    {
        var (servicio, _, _) = Crear();
        var empleadoId = Guid.NewGuid();

        await servicio.CrearAsync(new CrearUsuarioRequest("uno@gira.com", "Uno", empleadoId));
        var segundo = await servicio.CrearAsync(new CrearUsuarioRequest("dos@gira.com", "Dos", null));

        var r = await servicio.ActualizarAsync(
            segundo.Value!.Usuario.Id,
            new ActualizarUsuarioRequest("dos@gira.com", "Dos", empleadoId));

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.Conflict, r.Type);
    }

    [Fact]
    public async Task Actualizar_ConservandoSuPropioEmpleado_Funciona()
    {
        var (servicio, _, _) = Crear();
        var empleadoId = Guid.NewGuid();

        var creado = await servicio.CrearAsync(new CrearUsuarioRequest("uno@gira.com", "Uno", empleadoId));

        var r = await servicio.ActualizarAsync(
            creado.Value!.Usuario.Id,
            new ActualizarUsuarioRequest("uno@gira.com", "Uno Editado", empleadoId));

        Assert.True(r.IsSuccess);
    }
}