using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Authorization
{
    public class RespuestaAutorizacionHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _predeterminado = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Challenged)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "No autenticado",
                        Detail = "Debes iniciar sesión para acceder a este recurso."
                    },
                    options: null,
                    contentType: "application/problem+json");
                return;
            }

            if (authorizeResult.Forbidden)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Acceso denegado",
                        Detail = "Tu rol no tiene permiso para realizar esta acción."
                    },
                    options: null,
                    contentType: "application/problem+json");
                return;
            }

            await _predeterminado.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
