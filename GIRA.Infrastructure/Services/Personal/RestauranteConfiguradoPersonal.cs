using GIRA.Application.Interfaces.Services.Personal;
using Microsoft.Extensions.Configuration;

namespace GIRA.Infrastructure.Services.Personal;

/// <summary>
/// Lee el restaurante de la misma clave que usa Analítica (<c>Analitica:RestauranteId</c>). Si falta o no es un GUID válido,
/// devuelve null y Personal descarta los eventos en vez de fallar.
/// </summary>
public class RestauranteConfiguradoPersonal : IRestauranteConfigurado
{
    private readonly IConfiguration _configuracion;

    /// <summary>Crea el lector con la configuración de la aplicación.</summary>
    public RestauranteConfiguradoPersonal(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    /// <inheritdoc />
    public Guid? RestauranteId =>
        Guid.TryParse(_configuracion["Analitica:RestauranteId"], out var id) && id != Guid.Empty ? id : null;
}
