using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Analitica;

public sealed class PrediccionAfluenciaEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private readonly Guid _franja = Guid.NewGuid();
    private readonly Guid _otraFranja = Guid.NewGuid();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        db.FranjasHorarias.AddRange(
            new FranjaHoraria { Id = _franja, Nombre = "Almuerzo" },
            new FranjaHoraria { Id = _otraFranja, Nombre = "Cena" });
        db.Set<PrediccionAfluencia>().AddRange(
            Prediccion(new DateOnly(2026, 11, 21), _franja, 8, true),
            Prediccion(new DateOnly(2026, 11, 21), _franja, 90, false),
            Prediccion(new DateOnly(2026, 11, 21), _otraFranja, 3, true),
            Prediccion(new DateOnly(2026, 11, 22), _franja, 5, true));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Consulta_FiltraFechaFranjaYVigencia_ConservaMetadatos()
    {
        Como(Roles.Supervisor);
        var resultados = await _client.GetFromJsonAsync<List<PrediccionAfluenciaDto>>(
            $"/api/analitica/prediccion?desde=2026-11-21&hasta=2026-11-21&franjaId={_franja}");
        var resultado = Assert.Single(resultados!);
        Assert.Equal(8, resultado.AfluenciaEstimada);
        Assert.Equal("Almuerzo", resultado.Franja);
        Assert.True(resultado.BajaConfianza);
        Assert.Null(resultado.LimiteInferior);
        Assert.Equal("base-1", resultado.VersionModelo);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero), resultado.GeneradaEn);
    }

    [Fact]
    public async Task Consulta_SinDatos_DevuelveListaVacia()
    {
        Como(Roles.Gerente);
        var resultados = await _client.GetFromJsonAsync<List<PrediccionAfluenciaDto>>(
            "/api/analitica/prediccion?desde=2026-12-01&hasta=2026-12-01");
        Assert.Empty(resultados!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?desde=2026-11-22&hasta=2026-11-21")]
    [InlineData("?desde=incorrecta&hasta=2026-11-21")]
    [InlineData("?desde=2026-01-01&hasta=2027-01-02")]
    [InlineData("?desde=2026-11-21&hasta=2026-11-21&franjaId=00000000-0000-0000-0000-000000000000")]
    public async Task Consulta_ParametrosInvalidos_Responde400(string query)
    {
        Como(Roles.Gerente);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/analitica/prediccion" + query)).StatusCode);
    }

    [Fact]
    public async Task Consulta_ExigeSupervisorOGerencia()
    {
        const string url = "/api/analitica/prediccion?desde=2026-11-21&hasta=2026-11-21";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Mesero);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Administrador);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(url)).StatusCode);
    }

    private static PrediccionAfluencia Prediccion(DateOnly fecha, Guid franja, int personas, bool vigente) => new()
    {
        Id = Guid.NewGuid(), GeneracionId = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = franja,
        AfluenciaEstimada = personas, BajaConfianza = true, GeneradaEn = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
        VersionModelo = "base-1", Vigente = vigente
    };

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        _client.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [rol]).Token);
    }
}
