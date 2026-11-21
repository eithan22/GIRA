using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Analitica;

public sealed class HistorialAfluenciaEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private HttpClient _client = null!;
    private readonly Guid _franja = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var dia = new DateOnly(2026, 11, 18);
        db.Set<RegistroAfluencia>().AddRange(
            Registro(dia, OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.VisitaCompletada, 4),
            Registro(dia, OrigenRegistroAfluencia.VisitaSinReserva, TipoRegistroAfluencia.VisitaCompletada, 3),
            Registro(dia, OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.ReservaNoAsistida, 5),
            Registro(dia.AddDays(-1), OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.VisitaCompletada, 8));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Consulta_AgrupaComensalesYCuentaNoShows_SinSumarlosComoAsistencia()
    {
        Como(Roles.Gerente);
        var respuesta = await _client.GetAsync("/api/analitica/historial?desde=2026-11-18&hasta=2026-11-18");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var item = Assert.Single((await respuesta.Content.ReadFromJsonAsync<List<HistorialAfluenciaDto>>())!);
        Assert.Equal(_franja, item.FranjaHorariaId);
        Assert.Equal(4, item.ComensalesConReserva);
        Assert.Equal(3, item.ComensalesSinReserva);
        Assert.Equal(7, item.TotalComensales);
        Assert.Equal(1, item.ReservasCompletadas);
        Assert.Equal(1, item.ReservasNoAsistidas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?desde=2026-11-19&hasta=2026-11-18")]
    [InlineData("?desde=incorrecta&hasta=2026-11-18")]
    public async Task Consulta_Invalida_Responde400(string query)
    {
        Como(Roles.Administrador);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/analitica/historial" + query)).StatusCode);
    }

    [Fact]
    public async Task Consulta_ExigeRolGerencial()
    {
        const string url = "/api/analitica/historial?desde=2026-11-18&hasta=2026-11-18";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Administrador);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Consulta_IntervaloSinRegistros_DevuelveListaVacia()
    {
        Como(Roles.Gerente);

        var respuesta = await _client.GetAsync("/api/analitica/historial?desde=2026-10-01&hasta=2026-10-02");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<List<HistorialAfluenciaDto>>())!);
    }

    [Fact]
    public async Task Consulta_SeparaFechasYFranjasSinIncluirOtrosDias()
    {
        var otraFranja = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            var registro = Registro(new DateOnly(2026, 11, 18), OrigenRegistroAfluencia.Reserva,
                TipoRegistroAfluencia.VisitaCompletada, 2);
            registro.FranjaHorariaId = otraFranja;
            db.Set<RegistroAfluencia>().Add(registro);
            await db.SaveChangesAsync();
        }
        Como(Roles.Gerente);

        var respuesta = await _client.GetAsync("/api/analitica/historial?desde=2026-11-17&hasta=2026-11-18");
        var registros = (await respuesta.Content.ReadFromJsonAsync<List<HistorialAfluenciaDto>>())!;

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(3, registros.Count);
        Assert.Contains(registros, r => r.Fecha == new DateOnly(2026, 11, 17) && r.TotalComensales == 8);
        Assert.Contains(registros, r => r.FranjaHorariaId == otraFranja && r.TotalComensales == 2);
        Assert.Contains(registros, r => r.Fecha == new DateOnly(2026, 11, 18) &&
                                       r.FranjaHorariaId == _franja && r.TotalComensales == 7);
    }

    [Fact]
    public async Task Consulta_FalloDelRepositorio_RespondeErrorSinExponerDetalles()
    {
        await using var fabrica = new FabricaConRepositorioFallido();
        using var cliente = fabrica.CreateClient();
        using var scope = fabrica.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [Roles.Gerente]).Token);

        var respuesta = await cliente.GetAsync("/api/analitica/historial?desde=2026-11-18&hasta=2026-11-18");
        var contenido = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.DoesNotContain("fallo interno de prueba", contenido);
    }

    private sealed class FabricaConRepositorioFallido : GiraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRegistroAfluenciaRepository>();
                services.AddScoped<IRegistroAfluenciaRepository, RepositorioFallido>();
            });
        }
    }

    private sealed class RepositorioFallido : IRegistroAfluenciaRepository
    {
        public Task RegistrarAsync(RegistroAfluencia registro, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fallo interno de prueba");

        public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
            DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fallo interno de prueba");
    }

    private RegistroAfluencia Registro(DateOnly fecha, OrigenRegistroAfluencia origen, TipoRegistroAfluencia tipo, int personas) => new()
    {
        Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = _franja,
        Origen = origen, Tipo = tipo, Personas = personas
    };

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        _client.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [rol]).Token);
    }
}
