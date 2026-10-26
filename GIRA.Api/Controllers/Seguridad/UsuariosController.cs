using GIRA.Api.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers;

/// <summary>Gestión de las cuentas de usuario del personal (GI-98). Solo Administrador.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.GestionarUsuarios)]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarios;

    public UsuariosController(IUsuarioService usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>Lista paginada, filtrable por rol y estado.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] UsuarioFiltroRequest filtro, CancellationToken ct)
        => (await _usuarios.ListarAsync(filtro, ct)).ToActionResult();

    /// <summary>Obtiene un usuario por su identificador.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _usuarios.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea una cuenta con contraseña temporal generada.</summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioRequest request, CancellationToken ct)
        => (await _usuarios.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Actualiza nombre, correo y empleado vinculado.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarUsuarioRequest request, CancellationToken ct)
        => (await _usuarios.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva un usuario.</summary>
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest request, CancellationToken ct)
        => (await _usuarios.CambiarEstadoAsync(id, request.Activo, ct)).ToActionResult();

    /// <summary>Reemplaza los roles del usuario (GI-99); el cambio se refleja en su siguiente token.</summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> AsignarRoles(Guid id, [FromBody] AsignarRolesRequest request, CancellationToken ct)
        => (await _usuarios.AsignarRolesAsync(id, request, ct)).ToActionResult();
}
