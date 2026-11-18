using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Reservas;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Inventario;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static GIRA.Tests.Inventario.ApiInventario;

namespace GIRA.Tests.Inventario;

/// <summary>Salidas teóricas por visita (GI-463, GI-462) y lotes con su proceso diario de vencimientos (GI-484, GI-483).</summary>
public class SalidasYVencimientosTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 11, 18);
    private readonly AlertasPublicadas _publicadas = new();
    private readonly GiraApiFactory _factory;
    private HttpClient _cliente = null!;

    public SalidasYVencimientosTests()
    {
        // 18/11/2026 11:00 en Santo Domingo.
        _factory = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero), services =>
            services.AddSingleton<INotificationHandler<AlertaInventarioGeneradaEvent>>(_publicadas));
    }

    public Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    // --- Salidas teóricas (RF-INV-08, RN-INV-02) ---

    [Fact]
    public async Task Una_Visita_Completada_Genera_Salidas_Teoricas_Por_Insumo_Caso_De_Prueba_4()
    {
        var (arroz, pollo) = await MenuAsync();
        var visita = Guid.NewGuid();

        await PublicarAsync(_factory, new VisitaCompletadaEvent(Hoy, Guid.NewGuid(), 5, ConReserva: true, VisitaId: visita));

        // Arroz: 60 % × 2 kg ÷ 4 porciones = 0.3 kg por persona → 1.5 kg.
        // Pollo: 60 % × 1 kg ÷ 4 + 40 % × 0.5 kg ÷ 1 = 0.35 kg por persona → 1.75 kg.
        var salidaArroz = Assert.Single(await SalidasTeoricasAsync(arroz));
        var salidaPollo = Assert.Single(await SalidasTeoricasAsync(pollo));
        Assert.Equal((-1.5m, 8.5m), (salidaArroz.Cantidad, salidaArroz.SaldoResultante));
        Assert.Equal((-1.75m, 8.25m), (salidaPollo.Cantidad, salidaPollo.SaldoResultante));
        Assert.All([salidaArroz, salidaPollo], s =>
        {
            Assert.Equal("Salida", s.Tipo);
            Assert.Null(s.Usuario);
            Assert.Equal("Visita con reserva del 18/11/2026, 5 personas", s.Referencia);
        });
    }

    [Fact]
    public async Task El_Mismo_Evento_Repetido_No_Descuenta_Dos_Veces_Y_Otra_Visita_Igual_Si()
    {
        var (arroz, _) = await MenuAsync();
        var evento = new VisitaCompletadaEvent(Hoy, Guid.NewGuid(), 2, ConReserva: true, VisitaId: Guid.NewGuid());

        await PublicarAsync(_factory, evento);
        await PublicarAsync(_factory, evento);
        Assert.Equal(9.4m, await StockAsync(_cliente, arroz));

        // Mismos datos, otra visita: sí se descuenta.
        await PublicarAsync(_factory, evento with { VisitaId = Guid.NewGuid() });
        Assert.Equal(8.8m, await StockAsync(_cliente, arroz));
        Assert.Equal(2, (await SalidasTeoricasAsync(arroz)).Count);
    }

    [Fact]
    public async Task Una_Visita_Sin_Reserva_Descuenta_Lo_Mismo_Que_Una_Con_Reserva()
    {
        var (arroz, pollo) = await MenuAsync();

        await PublicarAsync(_factory, new VisitaCompletadaEvent(Hoy, Guid.NewGuid(), 4, ConReserva: true, VisitaId: Guid.NewGuid()));
        await PublicarAsync(_factory, new VisitaCompletadaEvent(Hoy, Guid.NewGuid(), 4, ConReserva: false, VisitaId: Guid.NewGuid()));

        foreach (var insumo in new[] { arroz, pollo })
        {
            var salidas = await SalidasTeoricasAsync(insumo);
            Assert.Equal(2, salidas.Count);
            Assert.Equal(salidas[0].Cantidad, salidas[1].Cantidad);
            Assert.EndsWith("4 personas", salidas[1].Referencia);
            Assert.StartsWith("Visita sin reserva", salidas[1].Referencia);
        }
    }

    [Fact]
    public async Task Si_No_Alcanza_El_Stock_Queda_En_Cero_Y_Se_Abre_Una_Inconsistencia()
    {
        var (arroz, _) = await MenuAsync();

        // 40 personas × 0.3 kg = 12 kg y solo hay 10.
        await PublicarAsync(_factory, new VisitaCompletadaEvent(Hoy, Guid.NewGuid(), 40, ConReserva: true, VisitaId: Guid.NewGuid()));

        Assert.Equal(0, await StockAsync(_cliente, arroz));
        Assert.Equal(-10m, Assert.Single(await SalidasTeoricasAsync(arroz)).Cantidad);
        Assert.Contains(await AlertasAsync(_cliente), a => a.InsumoId == arroz && a.Tipo == "Inconsistencia");
        Assert.Contains(_publicadas.Eventos, e => e.InsumoId == arroz && e.Tipo == TipoAlertaInventario.Inconsistencia);
    }

    // --- Lotes y vencimientos (RF-INV-14, RN-INV-11) ---

    [Fact]
    public async Task Una_Entrada_De_Un_Perecedero_Pide_Fecha_De_Vencimiento_Y_Crea_El_Lote()
    {
        var leche = await CrearInsumoAsync(_cliente, "Leche", 0, perecedero: true);

        var sinFecha = await Movimiento(_cliente, leche.Id, "Entrada", 6);
        var vencida = await Movimiento(_cliente, leche.Id, "Entrada", 6, vence: Hoy.AddDays(-1));
        var conFecha = await Movimiento(_cliente, leche.Id, "Entrada", 6, vence: Hoy.AddDays(7));

        Assert.Equal(HttpStatusCode.BadRequest, sinFecha.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, vencida.StatusCode);
        Assert.Equal(HttpStatusCode.OK, conFecha.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var lote = await scope.ServiceProvider.GetRequiredService<GiraDbContext>().LotesInsumo.SingleAsync(l => l.InsumoId == leche.Id);
        Assert.Equal((6m, Hoy, Hoy.AddDays(7)), (lote.Cantidad, lote.FechaEntrada, lote.FechaVencimiento));
    }

    [Fact]
    public async Task El_Proceso_Diario_Avisa_Los_Lotes_Que_Vencen_En_2_Dias_O_Menos_Y_No_Duplica()
    {
        var unDia = await PerecederoConLoteAsync("Leche", Hoy.AddDays(1));
        var dosDias = await PerecederoConLoteAsync("Queso", Hoy.AddDays(2));
        var cincoDias = await PerecederoConLoteAsync("Yogur", Hoy.AddDays(5));

        await ProcesoDiario().EjecutarCicloAsync(default);
        await ProcesoDiario().EjecutarCicloAsync(default);

        var alertas = await VencimientosAsync();
        Assert.Equal(new[] { unDia, dosDias }.Order(), alertas.Select(a => a.InsumoId).Order());
        Assert.DoesNotContain(alertas, a => a.InsumoId == cincoDias);
        Assert.Equal(2, _publicadas.Eventos.Count(e => e.Tipo == TipoAlertaInventario.Vencimiento));
        // Abrir el panel tampoco duplica.
        Assert.Equal(2, (await AlertasAsync(_cliente)).Count(a => a.Tipo == "Vencimiento"));
    }

    [Fact]
    public async Task Los_Dias_De_Aviso_Se_Configuran()
    {
        var cincoDias = await PerecederoConLoteAsync("Yogur", Hoy.AddDays(5));
        await ProcesoDiario().EjecutarCicloAsync(default);
        Assert.Empty(await VencimientosAsync());

        (await _cliente.PutAsJsonAsync("/api/configuracion/inventario", new ActualizarParametrosInventarioRequest(3, 5))).EnsureSuccessStatusCode();
        await ProcesoDiario().EjecutarCicloAsync(default);

        Assert.Equal(cincoDias, Assert.Single(await VencimientosAsync()).InsumoId);
    }

    [Fact]
    public async Task Un_Vencimiento_Atendido_No_Vuelve_A_Salir_Pero_Un_Lote_Nuevo_Si()
    {
        var leche = await PerecederoConLoteAsync("Leche", Hoy.AddDays(1));
        await ProcesoDiario().EjecutarCicloAsync(default);
        var alerta = Assert.Single(await VencimientosAsync());

        (await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Atendida"))).EnsureSuccessStatusCode();
        await ProcesoDiario().EjecutarCicloAsync(default);

        // El panel también revisa vencimientos: antes de la corrección volvía a abrir la misma alerta.
        Assert.DoesNotContain(await AlertasAsync(_cliente), a => a.InsumoId == leche);
        Assert.Single(await VencimientosAsync());

        (await Movimiento(_cliente, leche, "Entrada", 2, vence: Hoy.AddDays(2))).EnsureSuccessStatusCode();
        await ProcesoDiario().EjecutarCicloAsync(default);

        Assert.Equal(2, (await VencimientosAsync()).Count);
        Assert.Contains(await AlertasAsync(_cliente), a => a.InsumoId == leche && a.Tipo == "Vencimiento");
    }

    [Fact]
    public async Task Un_Lote_Ya_Gastado_No_Avisa()
    {
        var leche = await PerecederoConLoteAsync("Leche", Hoy.AddDays(1));
        await Movimiento(_cliente, leche, "Merma", 3, motivo: "Se derramó");

        await ProcesoDiario().EjecutarCicloAsync(default);

        Assert.Empty(await VencimientosAsync());
    }

    // Menú de prueba: Plato A (60 %, 4 porciones: 2 kg de arroz y 1 kg de pollo) y Plato B (40 %, 1 porción: 0.5 kg de pollo).
    private async Task<(Guid Arroz, Guid Pollo)> MenuAsync()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 0);
        var pollo = await CrearInsumoAsync(_cliente, "Pollo", 0);
        var a = (await CrearPlatoAsync(_cliente, "Plato A", 60)).Plato;
        var b = (await CrearPlatoAsync(_cliente, "Plato B", 40)).Plato;
        await GuardarRecetaAsync(_cliente, a.Id, 4, (arroz.Id, 2), (pollo.Id, 1));
        await GuardarRecetaAsync(_cliente, b.Id, 1, (pollo.Id, 0.5m));
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);
        await Movimiento(_cliente, pollo.Id, "Entrada", 10);
        return (arroz.Id, pollo.Id);
    }

    private async Task<List<MovimientoDto>> SalidasTeoricasAsync(Guid insumoId) =>
        (await _cliente.GetFromJsonAsync<KardexDto>($"/api/movimientos?insumoId={insumoId}&hasta={Hoy:yyyy-MM-dd}"))!
            .Movimientos.Items.Where(m => m.Teorico).ToList();

    private async Task<Guid> PerecederoConLoteAsync(string nombre, DateOnly vence)
    {
        var insumo = await CrearInsumoAsync(_cliente, nombre, 0, perecedero: true);
        (await Movimiento(_cliente, insumo.Id, "Entrada", 3, vence: vence)).EnsureSuccessStatusCode();
        return insumo.Id;
    }

    private InventarioProcesoVencimientosService ProcesoDiario() => new(
        _factory.Services.GetRequiredService<IServiceScopeFactory>(),
        _factory.Services.GetRequiredService<TimeProvider>(),
        NullLogger<InventarioProcesoVencimientosService>.Instance);

    // Directo de la base: GET /api/inventario/alertas también revisa vencimientos y taparía lo que hizo el proceso.
    private async Task<List<Domain.Entities.Inventario.AlertaInventario>> VencimientosAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GiraDbContext>().AlertasInventario
            .Where(a => a.Tipo == TipoAlertaInventario.Vencimiento).ToListAsync();
    }
}
