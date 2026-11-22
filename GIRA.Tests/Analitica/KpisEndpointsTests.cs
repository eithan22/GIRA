using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Analitica;

public sealed class KpisEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private readonly Guid _franja = Guid.NewGuid();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        db.FranjasHorarias.Add(new FranjaHoraria { Id = _franja, Nombre = "Almuerzo" });
        db.Set<RegistroAfluencia>().AddRange(
            Registro(new DateOnly(2026, 11, 2), OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.VisitaCompletada, 8),
            Registro(new DateOnly(2026, 11, 2), OrigenRegistroAfluencia.VisitaSinReserva, TipoRegistroAfluencia.VisitaCompletada, 2),
            Registro(new DateOnly(2026, 11, 2), OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.ReservaNoAsistida, 5),
            Registro(new DateOnly(2026, 11, 9), OrigenRegistroAfluencia.Reserva, TipoRegistroAfluencia.VisitaCompletada, 20));
        var cancelada = new Reserva { Id = Guid.NewGuid() };
        cancelada.Cancelar(null);
        var creada = new Reserva { Id = Guid.NewGuid() };
        var anterior = new Reserva { Id = Guid.NewGuid() };
        var posterior = new Reserva { Id = Guid.NewGuid() };
        posterior.Cancelar(null);
        db.Reservas.AddRange(cancelada, creada, anterior, posterior);
        var abierta = new AlertaInventario { Id = Guid.NewGuid() };
        var cerrada = new AlertaInventario { Id = Guid.NewGuid() };
        cerrada.Cerrar(EstadoAlertaInventario.Atendida, null, DateTime.UtcNow);
        db.AlertasInventario.AddRange(abierta, cerrada);
        var insumoId = Guid.NewGuid();
        db.Insumos.Add(new Insumo { Id = insumoId, Nombre = "Arroz" });
        db.MovimientosInventario.AddRange(
            Movimiento(insumoId, TipoMovimientoInventario.Salida, -2m, 50m),
            Movimiento(insumoId, TipoMovimientoInventario.Merma, -1m, 50m));
        await db.SaveChangesAsync();

        cancelada.CreatedAt = new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);
        creada.CreatedAt = new DateTime(2026, 11, 16, 3, 59, 59, DateTimeKind.Utc);
        anterior.CreatedAt = new DateTime(2026, 11, 1, 3, 59, 59, DateTimeKind.Utc);
        posterior.CreatedAt = new DateTime(2026, 11, 16, 4, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Consulta_CalculaIndicadoresYPromedioDeFechasObservadas()
    {
        Como(Roles.Gerente);
        var respuesta = await _client.GetAsync("/api/analitica/kpis?desde=2026-11-01&hasta=2026-11-15");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var kpis = (await respuesta.Content.ReadFromJsonAsync<KpisDto>())!;

        Assert.Equal(30, kpis.ComensalesAtendidos);
        Assert.Equal(100m / 3m, kpis.TasaNoShow);
        Assert.Equal(50m, kpis.TasaCancelacion);
        Assert.Equal(20m / 3m, kpis.ProporcionWalkIns);
        Assert.Equal(5m, kpis.CostoInsumosPorComensal);
        Assert.Equal(100m / 3m, kpis.Merma);
        Assert.Equal(1, kpis.AlertasAbiertas);
        var promedio = Assert.Single(kpis.AfluenciaPromedio);
        Assert.Equal(DayOfWeek.Monday, promedio.DiaSemana);
        Assert.Equal(_franja, promedio.FranjaHorariaId);
        Assert.Equal("Almuerzo", promedio.Franja);
        Assert.Equal(15m, promedio.Promedio);
        Assert.Equal(2, promedio.FechasObservadas);
        Assert.Null(kpis.TasaOcupacion);
        Assert.Null(kpis.PrecisionModelo);
        Assert.Null(kpis.HorasPersonalPorComensal);
    }

    [Fact]
    public async Task Consulta_SinDatos_NoInventaPorcentajes()
    {
        Como(Roles.Administrador);
        var kpis = await _client.GetFromJsonAsync<KpisDto>("/api/analitica/kpis?desde=2026-09-01&hasta=2026-09-30");
        Assert.NotNull(kpis);
        Assert.Null(kpis.ComensalesAtendidos);
        Assert.Null(kpis.TasaNoShow);
        Assert.Null(kpis.TasaCancelacion);
        Assert.Null(kpis.ProporcionWalkIns);
        Assert.Empty(kpis.AfluenciaPromedio);
        Assert.Equal(1, kpis.AlertasAbiertas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?desde=2026-11-19&hasta=2026-11-18")]
    [InlineData("?desde=incorrecta&hasta=2026-11-18")]
    [InlineData("?desde=2026-11-18&hasta=9999-12-31")]
    public async Task Consulta_RangoInvalido_Responde400(string query)
    {
        Como(Roles.Gerente);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/analitica/kpis" + query)).StatusCode);
    }

    [Fact]
    public async Task Consulta_ExigeRolGerencial()
    {
        const string url = "/api/analitica/kpis?desde=2026-11-01&hasta=2026-11-15";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(url)).StatusCode);
        Como(Roles.Administrador);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(url)).StatusCode);
    }

    private RegistroAfluencia Registro(DateOnly fecha, OrigenRegistroAfluencia origen, TipoRegistroAfluencia tipo, int personas) => new()
    {
        Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = _franja,
        Origen = origen, Tipo = tipo, Personas = personas
    };

    private static MovimientoInventario Movimiento(
        Guid insumoId, TipoMovimientoInventario tipo, decimal cantidad, decimal costoUnitario) => new()
    {
        Id = Guid.NewGuid(),
        InsumoId = insumoId,
        Tipo = tipo,
        Cantidad = cantidad,
        CostoUnitario = costoUnitario,
        Fecha = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc)
    };

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        _client.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [rol]).Token);
    }
}
