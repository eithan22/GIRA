using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GIRA.Api.Authorization
{
    public static class AutorizacionExtensions
    {
        /// <summary>Registra todas las políticas de <see cref="Politicas"/> con los roles que las cumplen.</summary>
        public static IServiceCollection AddAutorizacionGira(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Deny-by-default: un endpoint sin [Authorize] ni [AllowAnonymous] exige
                // un usuario autenticado, no queda público por accidente.
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                void Agregar(string politica, params string[] roles) =>
                    options.AddPolicy(politica, p => p.RequireAuthenticatedUser().RequireRole(roles));

                // Seguridad
                Agregar(Politicas.GestionarUsuarios, Roles.Administrador);

                // Reservas
                Agregar(Politicas.OperarReservas, Roles.Administrador, Roles.Gerente, Roles.Supervisor, Roles.Mesero);
                Agregar(Politicas.GestionarSalon, Roles.Administrador, Roles.Gerente);

                // Inventario
                Agregar(Politicas.ConsultarInventario, Roles.Administrador, Roles.Gerente, Roles.Supervisor, Roles.Cocina);
                Agregar(Politicas.GestionarInventario, Roles.Administrador, Roles.Gerente);
                Agregar(Politicas.RegistrarMovimientos, Roles.Administrador, Roles.Gerente, Roles.Supervisor, Roles.Cocina);
                Agregar(Politicas.AtenderAlertasInventario, Roles.Administrador, Roles.Gerente, Roles.Supervisor);

                // Personal
                Agregar(Politicas.GestionarPersonal, Roles.Administrador, Roles.Gerente);
                Agregar(Politicas.AsignarTurnos, Roles.Administrador, Roles.Gerente, Roles.Supervisor);
                Agregar(Politicas.ConsultarMisTurnos, Roles.Todos);

                // Analítica
                Agregar(Politicas.VerDashboard, Roles.Administrador, Roles.Gerente);
                Agregar(Politicas.VerPrediccion, Roles.Administrador, Roles.Gerente, Roles.Supervisor);
                Agregar(Politicas.GestionarModelos, Roles.Administrador, Roles.Gerente);

                // Configuración
                Agregar(Politicas.GestionarConfiguracion, Roles.Administrador, Roles.Gerente);
                Agregar(Politicas.GestionarPlantillasCorreo, Roles.Administrador);
            });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, RespuestaAutorizacionHandler>();

            return services;
        }
    }
}
