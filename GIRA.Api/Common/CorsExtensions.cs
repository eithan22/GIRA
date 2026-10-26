namespace GIRA.Api.Common;

/// <summary>
/// Configura CORS a partir de la configuración ("Cors:OrigenesPermitidos"), con
/// <c>http://localhost:5173</c> como valor por defecto si la sección no existe.
/// </summary>
public static class CorsExtensions
{
    /// <summary>Nombre de la política que se aplica con <c>app.UseCors</c>.</summary>
    public const string Politica = "GiraFrontend";

    private static readonly string[] OrigenesPorDefecto = ["http://localhost:5173"];

    /// <summary>
    /// Registra la política de CORS con la lista explícita de orígenes. Nunca acepta
    /// <c>"*"</c> porque la política usa <c>AllowCredentials</c>; falla rápido si la lista
    /// queda vacía o si algún origen no es una URL absoluta válida.
    /// </summary>
    public static IServiceCollection AddGiraCors(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("Cors:OrigenesPermitidos");
        var origenes = seccion.Exists() ? seccion.Get<string[]>() ?? [] : OrigenesPorDefecto;

        if (origenes.Length == 0)
        {
            throw new InvalidOperationException(
                "La lista de orígenes CORS ('Cors:OrigenesPermitidos') no puede quedar vacía.");
        }

        foreach (var origen in origenes)
        {
            if (origen == "*")
            {
                throw new InvalidOperationException(
                    "El origen CORS '*' no está permitido porque la política usa AllowCredentials.");
            }

            if (!Uri.TryCreate(origen, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    $"El origen CORS '{origen}' no es una URL absoluta válida.");
            }
        }

        services.AddCors(options =>
        {
            options.AddPolicy(Politica, policy =>
                policy.WithOrigins(origenes)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        return services;
    }
}
