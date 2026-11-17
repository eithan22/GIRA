using GIRA.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Api;

/// <summary>
/// <see cref="GiraApiFactory"/> con el reloj del servicio fijo en <c>ahoraUtc</c> (hora de Santo
/// Domingo = UTC-4) y, opcionalmente, servicios extra registrados (por ejemplo, handlers de eventos).
/// Los tokens se firman con la hora real, así que no les afecta el reloj simulado.
/// </summary>
public class GiraApiFactoryConReloj : GiraApiFactory
{
    private readonly DateTimeOffset _ahoraUtc;
    private readonly Action<IServiceCollection>? _configurar;

    /// <summary>Crea la fábrica con el instante UTC simulado y registros opcionales.</summary>
    public GiraApiFactoryConReloj(DateTimeOffset ahoraUtc, Action<IServiceCollection>? configurar = null)
    {
        _ahoraUtc = ahoraUtc;
        _configurar = configurar;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new TimeProviderFalso(_ahoraUtc));
            _configurar?.Invoke(services);
        });
    }
}
