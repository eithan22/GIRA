using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GIRA.Api.Common;

/// <summary>
/// Swagger UI con el botón "Authorize" para probar los endpoints con el access token (GI-125).
/// El documento OpenAPI lo genera Microsoft.AspNetCore.OpenApi; Swagger UI solo lo muestra.
/// </summary>
public static class SwaggerBearerExtensions
{
    /// <summary>Registra el documento OpenAPI con el esquema de seguridad Bearer (JWT).</summary>
    public static IServiceCollection AddOpenApiConBearer(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer<EsquemaBearerTransformer>());
        return services;
    }

    /// <summary>
    /// Publica Swagger UI en <c>/swagger</c>, leyendo el documento de <c>/openapi/v1.json</c>.
    /// Solo debe llamarse en Development, igual que <c>MapOpenApi()</c>.
    /// </summary>
    public static WebApplication UseSwaggerGira(this WebApplication app)
    {
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "GIRA API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}

/// <summary>
/// Agrega al documento OpenAPI el esquema "Bearer" y lo exige en las operaciones, para que
/// Swagger UI muestre el botón "Authorize" y mande el encabezado Authorization en cada prueba.
/// </summary>
internal sealed class EsquemaBearerTransformer : IOpenApiDocumentTransformer
{
    private const string Esquema = "Bearer";

    private readonly IAuthenticationSchemeProvider _proveedorEsquemas;

    public EsquemaBearerTransformer(IAuthenticationSchemeProvider proveedorEsquemas)
    {
        _proveedorEsquemas = proveedorEsquemas;
    }

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var esquemas = await _proveedorEsquemas.GetAllSchemesAsync();
        if (!esquemas.Any(e => e.Name == Esquema))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [Esquema] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description = "Pega solo el access token de POST /api/auth/login, sin la palabra Bearer."
            }
        };

        foreach (var operacion in document.Paths.Values.SelectMany(ruta => ruta.Operations ?? []))
        {
            operacion.Value.Security ??= [];
            operacion.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Esquema, document)] = []
            });
        }
    }
}
