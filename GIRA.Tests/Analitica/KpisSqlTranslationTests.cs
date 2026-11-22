using System.Data.Common;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GIRA.Tests.Analitica;

public sealed class KpisSqlTranslationTests
{
    [Theory]
    [InlineData("reservas")]
    [InlineData("alertas")]
    [InlineData("franjas")]
    [InlineData("historial")]
    [InlineData("predicciones")]
    [InlineData("alertas-recomendaciones")]
    [InlineData("capacidad")]
    [InlineData("costos")]
    [InlineData("horas-personal")]
    public async Task Consulta_SeTraduceParaSqlServerAntesDeAbrirConexion(string consulta)
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseSqlServer("Server=localhost;Database=NoSeConecta;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(new BloquearConexion())
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var kpis = new KpisRepository(contexto);

        await Assert.ThrowsAsync<ConexionBloqueadaException>(async () =>
        {
            switch (consulta)
            {
                case "reservas":
                    await kpis.ContarReservasAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, default);
                    break;
                case "alertas":
                    await kpis.ContarAlertasAbiertasAsync(default);
                    break;
                case "franjas":
                    await kpis.ObtenerFranjasAsync([Guid.NewGuid()], default);
                    break;
                case "historial":
                    await new RegistroAfluenciaRepository(contexto)
                        .ConsultarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), default);
                    break;
                case "predicciones":
                    await new PrediccionAfluenciaRepository(contexto)
                        .ConsultarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Guid.NewGuid(), default);
                    break;
                case "alertas-recomendaciones":
                    await new RecomendacionesRepository(contexto)
                        .ContarAlertasAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, default);
                    break;
                case "capacidad":
                    await kpis.ConsultarCapacidadAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), default);
                    break;
                case "costos":
                    await kpis.ConsultarCostosAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, default);
                    break;
                case "horas-personal":
                    await kpis.ConsultarHorasPersonalAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, default);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(consulta));
            }
        });
    }

    private sealed class ConexionBloqueadaException : Exception;

    private sealed class BloquearConexion : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new ConexionBloqueadaException();
    }
}
