
using System.Security.Claims;
using GIRA.Api.Controllers.Seguridad;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Tests.Seguridad.Auth;

public class CambiarPasswordControllerTests
{
    [Fact]
    public async Task CambiarPassword_Exitoso_Devuelve204()
    {
        var usuarioId = Guid.NewGuid();

        var servicio = new AuthServiceFalso();

        var controlador = new AuthController(servicio);

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(TiposClaim.UsuarioId, usuarioId.ToString())
            },
            "TestAuthentication");

        controlador.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identidad)
            }
        };

        var solicitud = new CambiarPasswordRequest
        {
            PasswordActual = "Temporal123!",
            PasswordNueva = "NuevaClave456!"
        };

        var respuesta = await controlador.CambiarPassword(
            solicitud,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(respuesta);
        Assert.Equal(usuarioId, servicio.UsuarioIdRecibido);
        Assert.Same(solicitud, servicio.SolicitudRecibida);
    }

    private class AuthServiceFalso : IAuthService
    {
        public Guid UsuarioIdRecibido { get; private set; }

        public CambiarPasswordRequest? SolicitudRecibida { get; private set; }

        public Task<OperationResult> CambiarPasswordAsync(
            Guid usuarioId,
            CambiarPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            UsuarioIdRecibido = usuarioId;
            SolicitudRecibida = request;

            return Task.FromResult(OperationResult.Success());
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
    }
}
