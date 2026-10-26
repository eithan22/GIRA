using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Roles;
using GIRA.Application.DTOs.Seguridad.Usuarios;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GIRA.Tests.Seguridad;

/// <summary>
/// Pruebas de la asignación de roles (GI-99, GI-141): reglas de AsignarRolesAsync y los
/// endpoints PUT /api/usuarios/{id}/roles y GET /api/roles de punta a punta.
/// </summary>
public class AsignarRolesTests : IAsyncLifetime
{
    private const string Password = "Clave-Segura1";
    private const string CorreoAdmin = "admin@gira.local";
    private const string CorreoEmpleado = "ana.perez@gira.local";

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private Guid _adminId;
    private Guid _empleadoId;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        _adminId = await _factory.CrearUsuarioAsync(CorreoAdmin, Password, true, Roles.Administrador);
        _empleadoId = await _factory.CrearUsuarioAsync(CorreoEmpleado, Password, true, Roles.Mesero);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<OperationResult<UsuarioDto>> AsignarAsync(Guid id, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IUsuarioService>();
        return await servicio.AsignarRolesAsync(id, new AsignarRolesRequest(roles));
    }

    private async Task<List<string>> RolesEnBaseAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await userManager.FindByIdAsync(id.ToString());
        return (await userManager.GetRolesAsync(usuario!)).Order().ToList();
    }

    private async Task UsarTokenDeAsync(string correo)
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(correo, Password));
    }

    // Roles válidos -> reemplaza los roles actuales por la lista enviada.
    [Fact]
    public async Task AsignarRoles_RolesValidos_ReemplazaLosRoles()
    {
        var resultado = await AsignarAsync(_empleadoId, Roles.Gerente, Roles.Supervisor);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new[] { Roles.Gerente, Roles.Supervisor }, resultado.Value!.Roles);
        Assert.Equal(new[] { Roles.Gerente, Roles.Supervisor }, await RolesEnBaseAsync(_empleadoId));
    }

    // Nombres repetidos o con otras mayúsculas -> cada rol se guarda una sola vez.
    [Fact]
    public async Task AsignarRoles_NombresRepetidosOConMayusculas_SeGuardanUnaVez()
    {
        var resultado = await AsignarAsync(_empleadoId, "gerente", "GERENTE", " Gerente ");

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new[] { Roles.Gerente }, await RolesEnBaseAsync(_empleadoId));
    }

    // Lista vacía -> el usuario debe quedar con al menos un rol; no se cambia nada.
    [Fact]
    public async Task AsignarRoles_ListaVacia_DevuelveErrorDeValidacion()
    {
        var resultado = await AsignarAsync(_empleadoId);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal(new[] { Roles.Mesero }, await RolesEnBaseAsync(_empleadoId));
    }

    // Rol inexistente -> error de validación que lo nombra; no se cambia nada.
    [Fact]
    public async Task AsignarRoles_RolInexistente_DevuelveErrorConSuNombre()
    {
        var resultado = await AsignarAsync(_empleadoId, Roles.Gerente, "Cajero");

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Contains("Cajero", resultado.Error);
        Assert.Equal(new[] { Roles.Mesero }, await RolesEnBaseAsync(_empleadoId));
    }

    // Usuario inexistente -> no encontrado.
    [Fact]
    public async Task AsignarRoles_UsuarioInexistente_DevuelveNoEncontrado()
    {
        var resultado = await AsignarAsync(Guid.NewGuid(), Roles.Gerente);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    // Último Administrador activo -> no se le puede quitar el rol Administrador.
    [Fact]
    public async Task AsignarRoles_QuitarAdministradorAlUltimoActivo_DevuelveConflicto()
    {
        var resultado = await AsignarAsync(_adminId, Roles.Gerente);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(new[] { Roles.Administrador }, await RolesEnBaseAsync(_adminId));
    }

    // Un Administrador desactivado no cuenta: el único activo sigue protegido.
    [Fact]
    public async Task AsignarRoles_OtroAdministradorDesactivado_NoCuenta()
    {
        await _factory.CrearUsuarioAsync("admin.inactivo@gira.local", Password, false, Roles.Administrador);

        var resultado = await AsignarAsync(_adminId, Roles.Gerente);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    // Con otro Administrador activo -> sí se puede quitar el rol.
    [Fact]
    public async Task AsignarRoles_QuitarAdministradorConOtroActivo_LoPermite()
    {
        await _factory.CrearUsuarioAsync("admin2@gira.local", Password, true, Roles.Administrador);

        var resultado = await AsignarAsync(_adminId, Roles.Gerente);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new[] { Roles.Gerente }, await RolesEnBaseAsync(_adminId));
    }

    // PUT como Administrador -> 200 y el siguiente token del usuario trae los roles nuevos.
    [Fact]
    public async Task PutRoles_ComoAdministrador_ElSiguienteTokenTraeLosRolesNuevos()
    {
        await UsarTokenDeAsync(CorreoAdmin);

        var respuesta = await _cliente.PutAsJsonAsync($"/api/usuarios/{_empleadoId}/roles",
            new AsignarRolesRequest([Roles.Gerente, Roles.Supervisor]));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var token = new JsonWebTokenHandler().ReadJsonWebToken(await _cliente.ObtenerTokenAsync(CorreoEmpleado, Password));
        Assert.Equal(new[] { Roles.Gerente, Roles.Supervisor },
            token.Claims.Where(c => c.Type == TiposClaim.Rol).Select(c => c.Value).Order());
    }

    // PUT quitando el rol al último Administrador -> 409.
    [Fact]
    public async Task PutRoles_QuitarAdministradorAlUltimoActivo_Devuelve409()
    {
        await UsarTokenDeAsync(CorreoAdmin);

        var respuesta = await _cliente.PutAsJsonAsync($"/api/usuarios/{_adminId}/roles",
            new AsignarRolesRequest([Roles.Gerente]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    // PUT sin token -> 401.
    [Fact]
    public async Task PutRoles_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.PutAsJsonAsync($"/api/usuarios/{_empleadoId}/roles",
            new AsignarRolesRequest([Roles.Gerente]));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // PUT de alguien que no es Administrador -> 403.
    [Fact]
    public async Task PutRoles_ComoMesero_Devuelve403()
    {
        await UsarTokenDeAsync(CorreoEmpleado);

        var respuesta = await _cliente.PutAsJsonAsync($"/api/usuarios/{_adminId}/roles",
            new AsignarRolesRequest([Roles.Mesero]));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // GET /api/roles como Administrador -> los 5 roles fijos.
    [Fact]
    public async Task GetRoles_ComoAdministrador_DevuelveLosCincoRoles()
    {
        await UsarTokenDeAsync(CorreoAdmin);

        var roles = await _cliente.GetFromJsonAsync<List<RolDto>>("/api/roles");

        Assert.Equal(Roles.Todos.Order(), roles!.Select(r => r.Nombre));
    }

    // GET /api/roles de alguien que no es Administrador -> 403.
    [Fact]
    public async Task GetRoles_ComoMesero_Devuelve403()
    {
        await UsarTokenDeAsync(CorreoEmpleado);

        var respuesta = await _cliente.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
