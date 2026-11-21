using GIRA.Api.Controllers.Seguridad;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Tests.Seguridad.Auth;

/// <summary>
/// GI-217: Pruebas de la acción POST /api/auth/olvide-password aisladas del servicio real:
/// comprueban que la solicitud llega al caso de uso y que la respuesta no distingue casos.
/// </summary>
public class OlvidePasswordControllerTests
{
    private static AuthController Crear(IAuthService servicio) =>
        new(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    // Caso normal -> 200 y la solicitud llega tal cual al servicio.
    [Fact]
    public async Task OlvidePassword_SolicitudValida_Devuelve200YLlamaAlServicio()
    {
        var servicio = new AuthServiceFalso(OperationResult.Success());
        var controlador = Crear(servicio);
        var solicitud = new OlvidePasswordRequest { Email = "ana.perez@gira.local" };

        var respuesta = await controlador.OlvidePassword(solicitud, CancellationToken.None);

        Assert.IsType<OkResult>(respuesta);
        Assert.Same(solicitud, servicio.SolicitudRecibida);
    }

    // El servicio responde éxito para un correo inexistente -> la respuesta es idéntica al caso normal.
    [Fact]
    public async Task OlvidePassword_CorreoInexistente_DevuelveLaMismaRespuesta()
    {
        var servicio = new AuthServiceFalso(OperationResult.Success());
        var controlador = Crear(servicio);

        var respuesta = await controlador.OlvidePassword(
            new OlvidePasswordRequest { Email = "nadie@gira.local" },
            CancellationToken.None);

        Assert.IsType<OkResult>(respuesta);
    }

    // Fallo de validación -> ProblemDetails 400, no un 200 engañoso.
    [Fact]
    public async Task OlvidePassword_CorreoInvalido_Devuelve400()
    {
        var servicio = new AuthServiceFalso(
            OperationResult.Failure(ErrorType.Validation, "El correo no tiene un formato válido."));
        var controlador = Crear(servicio);

        var respuesta = await controlador.OlvidePassword(
            new OlvidePasswordRequest { Email = "no-es-un-correo" },
            CancellationToken.None);

        var objeto = Assert.IsType<ObjectResult>(respuesta);
        Assert.Equal(StatusCodes.Status400BadRequest, objeto.StatusCode);
    }

    private class AuthServiceFalso : IAuthService
    {
        private readonly OperationResult _resultado;

        public AuthServiceFalso(OperationResult resultado)
        {
            _resultado = resultado;
        }

        public OlvidePasswordRequest? SolicitudRecibida { get; private set; }

        public Task<OperationResult> OlvidePasswordAsync(
            OlvidePasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            SolicitudRecibida = request;

            return Task.FromResult(_resultado);
        }

        public Task<OperationResult<LoginResponse>> LoginAsync(
            LoginRequest request,
            string ip,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OperationResult<LoginResponse>> RefreshAsync(
            string refreshToken,
            string ip,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OperationResult> LogoutAsync(
            Guid usuarioId,
            string refreshToken,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OperationResult> CambiarPasswordAsync(
            Guid usuarioId,
            CambiarPasswordRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
