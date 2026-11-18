using GIRA.Application;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Exceptions.Inventario;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Inventario;

/// <summary>
/// Borra y vuelve a migrar GIRA_INVENTARIO_TEST una vez por corrida. Requiere el User Secret
/// <c>ConnectionStrings:GiraInventarioTest</c> en GIRA.Tests (nunca GIRA_DB).
/// </summary>
public class InventarioSqlServerFixture : IAsyncLifetime
{
    private const string Base = "GIRA_INVENTARIO_TEST";

    /// <summary>Servicios de Application e Infrastructure contra la base de pruebas.</summary>
    public ServiceProvider Servicios { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var conexion = new ConfigurationBuilder().AddUserSecrets<InventarioSqlServerFixture>().Build()
            .GetConnectionString("GiraInventarioTest");
        if (string.IsNullOrWhiteSpace(conexion))
        {
            throw new InvalidOperationException("Falta la cadena de conexión 'GiraInventarioTest' en los User Secrets de GIRA.Tests.");
        }

        // Seguro: solo se borra si la base es, exactamente, la de pruebas de Inventario.
        if (!string.Equals(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion).InitialCatalog, Base, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Las pruebas de Inventario contra SQL Server solo pueden usar {Base}.");
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GiraDb"] = conexion,
            ["Jwt:Issuer"] = "GIRA.Tests",
            ["Jwt:Audience"] = "GIRA.Tests",
            ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
            ["Jwt:AccessTokenMinutos"] = "30",
            ["Jwt:RefreshTokenDias"] = "7"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddApplication();
        services.AddInfrastructure(configuracion);
        Servicios = services.BuildServiceProvider();

        await using var scope = Servicios.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await Servicios.DisposeAsync();
}

/// <summary>
/// Concurrencia contra SQL Server real: el <c>RowVersion</c> del insumo evita perder un movimiento (GI-447) y el índice
/// único de VisitaId evita descontar dos veces la misma visita (GI-462).
/// </summary>
[Trait("Category", "Integration")]
public class InventarioSqlServerIntegrationTests : IClassFixture<InventarioSqlServerFixture>
{
    private readonly ServiceProvider _servicios;

    public InventarioSqlServerIntegrationTests(InventarioSqlServerFixture fixture) => _servicios = fixture.Servicios;

    [Fact]
    public async Task Dos_Movimientos_Que_Leen_El_Mismo_Stock_Solo_Guarda_El_Primero()
    {
        var insumoId = await CrearInsumoAsync($"Arroz {Guid.NewGuid():N}");
        await using var a = _servicios.CreateAsyncScope();
        await using var b = _servicios.CreateAsyncScope();
        var repoA = a.ServiceProvider.GetRequiredService<IInsumoRepository>();
        var repoB = b.ServiceProvider.GetRequiredService<IInsumoRepository>();
        var insumoA = (await repoA.BuscarPorIdAsync(insumoId, default))!;
        var insumoB = (await repoB.BuscarPorIdAsync(insumoId, default))!;

        insumoA.AplicarMovimiento(5);
        insumoB.AplicarMovimiento(3);
        await repoA.GuardarCambiosAsync(default);

        await Assert.ThrowsAsync<InventarioConflictoConcurrenciaException>(() => repoB.GuardarCambiosAsync(default));
        Assert.Equal(5, await StockAsync(insumoId));
    }

    [Fact]
    public async Task Varias_Entradas_A_La_Vez_No_Pierden_Ninguna_Que_Respondio_Bien()
    {
        var insumoId = await CrearInsumoAsync($"Sal {Guid.NewGuid():N}");

        var resultados = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var scope = _servicios.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IMovimientoInventarioService>()
                .RegistrarAsync(new RegistrarMovimientoRequest(insumoId, "Entrada", 1, null, null, null, null), null, default);
        })));

        var guardadas = resultados.Count(r => r.IsSuccess);
        Assert.True(guardadas >= 1);
        Assert.All(resultados.Where(r => r.IsFailure), r => Assert.Equal(ErrorType.Conflict, r.Type));
        Assert.Equal(guardadas, await StockAsync(insumoId));
        await using var lectura = _servicios.CreateAsyncScope();
        var kardex = await lectura.ServiceProvider.GetRequiredService<GiraDbContext>().MovimientosInventario
            .Where(m => m.InsumoId == insumoId).OrderBy(m => m.SaldoResultante).Select(m => m.SaldoResultante).ToListAsync();
        Assert.Equal(Enumerable.Range(1, guardadas).Select(i => (decimal)i), kardex);
    }

    [Fact]
    public async Task La_Misma_Visita_Procesada_A_La_Vez_Descuenta_Una_Sola_Vez()
    {
        var insumoId = await CrearInsumoAsync($"Pollo {Guid.NewGuid():N}");
        await RecetaYEntradaAsync(insumoId);
        var visitaId = Guid.NewGuid();

        async Task<bool?> ProcesarAsync()
        {
            await using var scope = _servicios.CreateAsyncScope();
            try
            {
                return await scope.ServiceProvider.GetRequiredService<IMovimientoInventarioService>()
                    .RegistrarSalidasTeoricasAsync(new DateOnly(2026, 11, 18), 4, true, visitaId, default);
            }
            catch (InventarioConflictoConcurrenciaException)
            {
                return null; // El otro guardó primero; Reservas registra el fallo en el log.
            }
        }

        var resultados = await Task.WhenAll(Task.Run(ProcesarAsync), Task.Run(ProcesarAsync));

        Assert.Equal(1, resultados.Count(r => r == true));
        Assert.False(await ProcesarAsync());
        // 4 personas × 0.5 kg = 2 kg, una sola vez.
        Assert.Equal(8, await StockAsync(insumoId));
    }

    private async Task<Guid> CrearInsumoAsync(string nombre)
    {
        await using var scope = _servicios.CreateAsyncScope();
        var catalogos = scope.ServiceProvider.GetRequiredService<ICatalogoInventarioService>();
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var unidad = (await catalogos.CrearUnidadAsync(new GuardarUnidadMedidaRequest($"Kilo {sufijo}", sufijo), default)).Value!;
        var categoria = (await catalogos.CrearCategoriaInsumoAsync(new GuardarCategoriaRequest($"General {sufijo}"), default)).Value!;
        var insumo = await scope.ServiceProvider.GetRequiredService<IInsumoService>()
            .CrearAsync(new GuardarInsumoRequest(nombre, categoria.Id, unidad.Id, 0, 10, false, null), default);
        Assert.True(insumo.IsSuccess, insumo.Error);
        return insumo.Value!.Id;
    }

    // Plato propio del insumo (100 % de demanda, 1 porción de 0.5 kg) y 10 kg en stock.
    private async Task RecetaYEntradaAsync(Guid insumoId)
    {
        await using var scope = _servicios.CreateAsyncScope();
        var catalogos = scope.ServiceProvider.GetRequiredService<ICatalogoInventarioService>();
        var platos = scope.ServiceProvider.GetRequiredService<IPlatoService>();
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var categoria = (await catalogos.CrearCategoriaPlatoAsync(new GuardarCategoriaRequest($"Platos {sufijo}"), default)).Value!;
        var plato = (await platos.CrearAsync(new GuardarPlatoRequest($"Plato {sufijo}", categoria.Id, 300, 100, true), default)).Value!;
        Assert.True((await platos.GuardarRecetaAsync(plato.Plato.Id, new GuardarRecetaRequest(1, null, [new LineaRecetaRequest(insumoId, 0.5m)]), default)).IsSuccess);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IMovimientoInventarioService>()
            .RegistrarAsync(new RegistrarMovimientoRequest(insumoId, "Entrada", 10, null, null, null, null), null, default)).IsSuccess);
    }

    private async Task<decimal> StockAsync(Guid insumoId)
    {
        await using var scope = _servicios.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GiraDbContext>().Insumos
            .Where(i => i.Id == insumoId).Select(i => i.CantidadStock).SingleAsync();
    }
}
