using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Inventario;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using GIRA.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static GIRA.Tests.Inventario.ApiInventario;

namespace GIRA.Tests.Inventario;

/// <summary>Movimientos (GI-447), kardex (GI-451) y conteo físico (GI-477) por HTTP.</summary>
public class MovimientosTests : IAsyncLifetime
{
    // 18/11/2026 10:00 en Santo Domingo; el reloj se puede adelantar para tener movimientos en días distintos.
    private readonly TimeProviderAvanzable _reloj = new(new DateTimeOffset(2026, 11, 18, 14, 0, 0, TimeSpan.Zero));
    private readonly GiraApiFactory _factory;
    private HttpClient _cliente = null!;

    public MovimientosTests()
    {
        _factory = new GiraApiFactoryConReloj(_reloj.GetUtcNow(), services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_reloj);
        });
    }

    private static DateOnly Hoy => new(2026, 11, 18);

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        _cliente.UsarToken((await TokenDeUsuarioRealAsync(_factory, Roles.Gerente)).Token);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    // --- Movimientos (RF-INV-05, RN-INV-01 a RN-INV-03) ---

    [Fact]
    public async Task Una_Entrada_De_10_Sube_El_Stock_En_10_Y_Guarda_Quien_La_Hizo()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 4);

        var respuesta = await Movimiento(_cliente, arroz.Id, "Entrada", 10);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(14, (await LeerAsync<MovimientoDto>(respuesta)).SaldoResultante);
        Assert.Equal(14, await StockAsync(_cliente, arroz.Id));
        var ultima = (await KardexAsync(arroz.Id)).Movimientos.Items[^1];
        Assert.Equal(("Entrada", 10m, "Ana Pérez"), (ultima.Tipo, ultima.Cantidad, ultima.Usuario));
    }

    [Fact]
    public async Task Merma_Perecedera_Valora_El_Consumo_Por_FEFO_Y_Media_Ponderada()
    {
        var leche = await CrearInsumoAsync(_cliente, "Leche FEFO", 0, perecedero: true);
        await Movimiento(_cliente, leche.Id, "Entrada", 2, vence: Hoy.AddDays(1), costoUnitario: 4);
        await Movimiento(_cliente, leche.Id, "Entrada", 3, vence: Hoy.AddDays(2), costoUnitario: 8);

        var respuesta = await Movimiento(_cliente, leche.Id, "Merma", 3, motivo: "Derrame");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var merma = (await KardexAsync(leche.Id)).Movimientos.Items[^1];
        Assert.Equal("Merma", merma.Tipo);
        Assert.Equal(16m / 3m, merma.CostoUnitario);
    }

    [Fact]
    public async Task Salida_De_Stock_Percedero_Historico_Sin_Lotes_No_Falla_Y_Deja_Costo_No_Disponible()
    {
        var leche = await CrearInsumoAsync(_cliente, "Leche histórica", 0, perecedero: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            var insumo = await contexto.Insumos.FindAsync(leche.Id);
            insumo!.AplicarMovimiento(5);
            await contexto.SaveChangesAsync();
        }

        var respuesta = await Movimiento(_cliente, leche.Id, "Merma", 1, motivo: "Producto vencido");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var merma = (await KardexAsync(leche.Id)).Movimientos.Items[^1];
        Assert.Null(merma.CostoUnitario);
        Assert.Equal(4m, await StockAsync(_cliente, leche.Id));
    }

    [Fact]
    public async Task Merma_Sin_Motivo_O_Mayor_Que_El_Stock_Responde_400_Y_No_Cambia_El_Stock()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 5);

        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(_cliente, arroz.Id, "Merma", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(_cliente, arroz.Id, "Merma", 6, motivo: "Se dañó")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(_cliente, arroz.Id, "Ajuste", -6, motivo: "Recuento")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(_cliente, arroz.Id, "Salida", 1, motivo: "Manual")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Movimiento(_cliente, Guid.NewGuid(), "Entrada", 1)).StatusCode);

        Assert.Equal(5, await StockAsync(_cliente, arroz.Id));
        Assert.Single((await KardexAsync(arroz.Id)).Movimientos.Items);
    }

    [Theory]
    [InlineData(Roles.Mesero, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Cocina, HttpStatusCode.OK)]
    [InlineData(Roles.Supervisor, HttpStatusCode.OK)]
    public async Task Registrar_Movimientos_Segun_El_Rol(string rol, HttpStatusCode esperado)
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        _cliente.UsarToken(Token(_factory, rol));

        Assert.Equal(esperado, (await Movimiento(_cliente, arroz.Id, "Entrada", 1)).StatusCode);
    }

    // --- Kardex (RF-INV-06) ---

    [Fact]
    public async Task Kardex_Muestra_Los_Movimientos_En_Orden_Con_El_Saldo_De_Cada_Uno()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);
        _reloj.Avanzar(TimeSpan.FromMinutes(5));
        await Movimiento(_cliente, arroz.Id, "Merma", 3, motivo: "Se mojó");
        _reloj.Avanzar(TimeSpan.FromMinutes(5));
        await Movimiento(_cliente, arroz.Id, "Ajuste", -2, motivo: "Recuento");

        var kardex = await KardexAsync(arroz.Id);

        Assert.Equal(["Entrada", "Merma", "Ajuste"], kardex.Movimientos.Items.Select(m => m.Tipo));
        Assert.Equal([10m, 7m, 5m], kardex.Movimientos.Items.Select(m => m.SaldoResultante));
        Assert.Equal((0m, 10m, -3m, -2m, 5m), (kardex.SaldoInicial, kardex.Entradas, kardex.Mermas, kardex.Ajustes, kardex.StockActual));
    }

    [Fact]
    public async Task Kardex_Que_Empieza_Despues_Del_Primer_Movimiento_Arranca_Con_Ese_Saldo()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);
        _reloj.Avanzar(TimeSpan.FromDays(2));
        await Movimiento(_cliente, arroz.Id, "Merma", 4, motivo: "Vencido");

        var kardex = await KardexAsync(arroz.Id, Hoy.AddDays(1), Hoy.AddDays(2));

        Assert.Equal(10, kardex.SaldoInicial);
        var merma = Assert.Single(kardex.Movimientos.Items);
        Assert.Equal((-4m, 6m), (merma.Cantidad, merma.SaldoResultante));
    }

    [Fact]
    public async Task Kardex_Con_Rango_Invertido_400_Insumo_Inexistente_404_Y_Cocina_403()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);

        var invertido = await _cliente.GetAsync($"/api/movimientos?insumoId={arroz.Id}&desde={Hoy:yyyy-MM-dd}&hasta={Hoy.AddDays(-1):yyyy-MM-dd}");
        var inexistente = await _cliente.GetAsync($"/api/movimientos?insumoId={Guid.NewGuid()}");
        _cliente.UsarToken(Token(_factory, Roles.Cocina));
        var cocina = await _cliente.GetAsync($"/api/movimientos?insumoId={arroz.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cocina.StatusCode);
    }

    // --- Conteo físico (RF-INV-11) ---

    [Fact]
    public async Task Conteo_Genera_Un_Ajuste_Por_Diferencia_Positiva_O_Negativa_Y_Ninguno_Si_Coincide()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        var sal = await CrearInsumoAsync(_cliente, "Sal", 1);
        var aceite = await CrearInsumoAsync(_cliente, "Aceite", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);
        await Movimiento(_cliente, sal.Id, "Entrada", 4);
        await Movimiento(_cliente, aceite.Id, "Entrada", 2);

        var resultado = await LeerAsync<ConteoFisicoResultadoDto>(await _cliente.PostAsJsonAsync("/api/movimientos/conteo-fisico",
            new ConteoFisicoRequest("Conteo semanal", [new(arroz.Id, 12), new(sal.Id, 3.5m), new(aceite.Id, 2)])));

        Assert.Equal(2, resultado.AjustesGenerados);
        Assert.Equal([(arroz.Id, 2m), (sal.Id, -0.5m)], resultado.Ajustes.Select(a => (a.InsumoId, a.Cantidad)));
        Assert.Equal((12m, 3.5m, 2m), (await StockAsync(_cliente, arroz.Id), await StockAsync(_cliente, sal.Id), await StockAsync(_cliente, aceite.Id)));

        var ajuste = (await KardexAsync(sal.Id)).Movimientos.Items[^1];
        Assert.Equal(("Ajuste", "Conteo físico", "Conteo semanal", 3.5m), (ajuste.Tipo, ajuste.Referencia, ajuste.Motivo, ajuste.SaldoResultante));
        Assert.Single((await KardexAsync(aceite.Id)).Movimientos.Items);
    }

    [Fact]
    public async Task Conteo_Con_Un_Insumo_Inexistente_No_Guarda_Nada()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);

        var respuesta = await _cliente.PostAsJsonAsync("/api/movimientos/conteo-fisico",
            new ConteoFisicoRequest("Conteo semanal", [new(arroz.Id, 8), new(Guid.NewGuid(), 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(10, await StockAsync(_cliente, arroz.Id));
        Assert.Single((await KardexAsync(arroz.Id)).Movimientos.Items);
    }

    private async Task<KardexDto> KardexAsync(Guid insumoId, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var url = $"/api/movimientos?insumoId={insumoId}&hasta={(hasta ?? Hoy.AddDays(5)):yyyy-MM-dd}";
        if (desde is { } d)
        {
            url += $"&desde={d:yyyy-MM-dd}";
        }

        return (await _cliente.GetFromJsonAsync<KardexDto>(url))!;
    }
}
