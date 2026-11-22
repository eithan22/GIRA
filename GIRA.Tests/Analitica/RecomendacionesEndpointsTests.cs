using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Application.Services.Analitica;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Analitica;

public sealed class RecomendacionesEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private readonly DateOnly _fecha = new(2026, 11, 20);
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRecomendacionesRepository>();
        await repo.GuardarVigentesAsync([Candidata("una")], DateTimeOffset.UtcNow, default);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ConsultaYActualizaEstado_SinDuplicarNiReactivarDescartadas()
    {
        Como(Roles.Gerente);
        var lista = await _client.GetFromJsonAsync<List<RecomendacionDto>>("/api/analitica/recomendaciones?estado=Nueva");
        var recomendacion = Assert.Single(lista!);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PatchAsJsonAsync(
            $"/api/analitica/recomendaciones/{recomendacion.Id}", new ActualizarRecomendacionRequest("Descartada"))).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRecomendacionesRepository>();
            await repo.GuardarVigentesAsync([Candidata("una")], DateTimeOffset.UtcNow, default);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<GiraDbContext>().Set<GIRA.Domain.Entities.Analitica.Recomendacion>().ToListAsync());
        }
        Assert.Empty((await _client.GetFromJsonAsync<List<RecomendacionDto>>("/api/analitica/recomendaciones?estado=Nueva"))!);
        var descartada = Assert.Single((await _client.GetFromJsonAsync<List<RecomendacionDto>>("/api/analitica/recomendaciones?estado=Descartada"))!);
        Assert.Equal(recomendacion.Id, descartada.Id);
    }

    [Fact]
    public async Task Consulta_RechazaParametrosYAccesoNoAutorizado()
    {
        const string url = "/api/analitica/recomendaciones";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Administrador);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(url + "?estado=invalido")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync(
            $"{url}/{Guid.NewGuid()}", new ActualizarRecomendacionRequest("Nueva"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync(
            $"{url}/{Guid.NewGuid()}", new ActualizarRecomendacionRequest("Vista"))).StatusCode);
    }

    [Fact]
    public async Task Recalculo_NoMuestraRecomendacionesQueYaNoAplican()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRecomendacionesRepository>();
        await repo.GuardarVigentesAsync([], DateTimeOffset.UtcNow, default);
        Assert.Empty(await repo.ConsultarAsync(null, default));
    }

    private RecomendacionCalculada Candidata(string clave) =>
        new(clave, _fecha, "Personal", "Revisar personal", "Media", "Predicción");

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        _client.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [rol]).Token);
    }
}
