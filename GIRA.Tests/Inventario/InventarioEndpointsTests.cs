using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Inventario;

public class InventarioEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

    public Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("/api/insumos")]
    [InlineData("/api/platos")]
    [InlineData("/api/inventario/alertas")]
    [InlineData("/api/unidades-medida")]
    public async Task Sin_Token_Responde_401(string url)
    {
        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/api/insumos", Roles.Mesero, HttpStatusCode.Forbidden)]
    [InlineData("/api/insumos", Roles.Cocina, HttpStatusCode.OK)]
    [InlineData("/api/platos", Roles.Cocina, HttpStatusCode.OK)]
    [InlineData("/api/unidades-medida", Roles.Cocina, HttpStatusCode.OK)]
    [InlineData("/api/proveedores", Roles.Cocina, HttpStatusCode.Forbidden)]
    [InlineData("/api/proveedores", Roles.Gerente, HttpStatusCode.OK)]
    [InlineData("/api/inventario/alertas", Roles.Cocina, HttpStatusCode.Forbidden)]
    [InlineData("/api/inventario/alertas", Roles.Supervisor, HttpStatusCode.OK)]
    public async Task Cada_Rol_Ve_Solo_Lo_Suyo(string url, string rol, HttpStatusCode esperado)
    {
        _cliente.UsarToken(Token(_factory, rol));

        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cocina_No_Puede_Crear_Insumos()
    {
        _cliente.UsarToken(Token(_factory, Roles.Cocina));

        var respuesta = await _cliente.PostAsJsonAsync("/api/insumos",
            new GuardarInsumoRequest("Sal", Guid.NewGuid(), Guid.NewGuid(), 1, 1, false, null));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Entrada_Y_Merma_Cambian_El_Stock_Y_Quedan_En_El_Kardex()
    {
        _cliente.UsarToken(await TokenDeUsuarioRealAsync(Roles.Gerente));
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", reorden: 2);
        Assert.Equal(0, arroz.CantidadStock);

        Assert.Equal(HttpStatusCode.OK, (await Movimiento(_cliente, arroz.Id, "Entrada", 10)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(_cliente, arroz.Id, "Merma", 3)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Movimiento(_cliente, arroz.Id, "Merma", 3, motivo: "Se mojó")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Movimiento(_cliente, arroz.Id, "Merma", 100, motivo: "No alcanza")).StatusCode);

        var insumo = await _cliente.GetFromJsonAsync<InsumoDto>($"/api/insumos/{arroz.Id}");
        Assert.Equal(7, insumo!.CantidadStock);

        var kardex = await _cliente.GetFromJsonAsync<KardexDto>($"/api/movimientos?insumoId={arroz.Id}");
        Assert.Equal(0, kardex!.SaldoInicial);
        Assert.Equal(10, kardex.Entradas);
        Assert.Equal(-3, kardex.Mermas);
        Assert.Equal([10m, 7m], kardex.Movimientos.Items.Select(m => m.SaldoResultante));
        Assert.All(kardex.Movimientos.Items, m => Assert.Equal("Ana Pérez", m.Usuario));
    }

    [Fact]
    public async Task No_Se_Desactiva_Un_Insumo_De_Una_Receta_Activa_Y_La_Receta_Pide_Cantidad_Mayor_A_Cero()
    {
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        var pollo = await CrearInsumoAsync(_cliente, "Pollo", reorden: 1);
        var plato = await CrearPlatoAsync(_cliente, "Pollo guisado", demanda: 100);

        var cero = await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(4, null, [new LineaRecetaRequest(pollo.Id, 0)]));
        Assert.Equal(HttpStatusCode.BadRequest, cero.StatusCode);

        var receta = await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(4, "Guisar", [new LineaRecetaRequest(pollo.Id, 2)]));
        receta.EnsureSuccessStatusCode();
        Assert.Equal(0.5m, (await receta.Content.ReadFromJsonAsync<RecetaDto>())!.Lineas.Single().CantidadPorPorcion);

        var desactivar = await _cliente.PatchAsJsonAsync($"/api/insumos/{pollo.Id}/estado", new CambiarEstadoInsumoRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, desactivar.StatusCode);
        Assert.Contains("Pollo guisado", await desactivar.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cambiar_Las_Lineas_De_Una_Receta_Quita_Las_Que_Ya_No_Estan()
    {
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", reorden: 1);
        var aceite = await CrearInsumoAsync(_cliente, "Aceite", reorden: 1);
        var plato = await CrearPlatoAsync(_cliente, "Arroz blanco", demanda: 100);

        (await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 1), new LineaRecetaRequest(aceite.Id, 0.1m)]))).EnsureSuccessStatusCode();
        (await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 1.5m)]))).EnsureSuccessStatusCode();

        var receta = await _cliente.GetFromJsonAsync<RecetaDto>($"/api/platos/{plato.Id}/receta");
        var linea = Assert.Single(receta!.Lineas);
        Assert.Equal(arroz.Id, linea.InsumoId);
        Assert.Equal(1.5m, linea.Cantidad);
    }

    [Fact]
    public async Task Conteo_Fisico_Genera_Un_Ajuste_Por_Diferencia()
    {
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", reorden: 1);
        var sal = await CrearInsumoAsync(_cliente, "Sal", reorden: 1);
        await Movimiento(_cliente, arroz.Id, "Entrada", 10);
        await Movimiento(_cliente, sal.Id, "Entrada", 4);

        var respuesta = await _cliente.PostAsJsonAsync("/api/movimientos/conteo-fisico",
            new ConteoFisicoRequest("Conteo semanal", [new LineaConteoRequest(arroz.Id, 8), new LineaConteoRequest(sal.Id, 4)]));

        respuesta.EnsureSuccessStatusCode();
        var resultado = await respuesta.Content.ReadFromJsonAsync<ConteoFisicoResultadoDto>();
        var ajuste = Assert.Single(resultado!.Ajustes);
        Assert.Equal(-2, ajuste.Cantidad);
        Assert.Equal(8, (await _cliente.GetFromJsonAsync<InsumoDto>($"/api/insumos/{arroz.Id}"))!.CantidadStock);
    }

    [Fact]
    public async Task Stock_Bajo_Abre_Una_Alerta_Y_Una_Entrada_Que_Lo_Repone_La_Cierra()
    {
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        var queso = await CrearInsumoAsync(_cliente, "Queso", reorden: 5);

        await Movimiento(_cliente, queso.Id, "Entrada", 3);
        await Movimiento(_cliente, queso.Id, "Entrada", 1);

        var abiertas = await _cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas?estado=Abierta");
        var alerta = Assert.Single(abiertas!.Items);
        Assert.Equal("StockBajo", alerta.Tipo);

        await Movimiento(_cliente, queso.Id, "Entrada", 10);

        Assert.Empty((await _cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas?estado=Abierta"))!.Items);
        var atendida = Assert.Single((await _cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas?estado=Atendida"))!.Items);
        Assert.Equal("Sistema", atendida.AtendidaPor);
    }

    [Fact]
    public async Task Una_Alerta_Se_Descarta_Una_Sola_Vez()
    {
        _cliente.UsarToken(await TokenDeUsuarioRealAsync(Roles.Gerente));
        var queso = await CrearInsumoAsync(_cliente, "Queso", reorden: 5);
        await Movimiento(_cliente, queso.Id, "Entrada", 1);
        var alerta = (await _cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas"))!.Items.Single();

        var primera = await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Descartada"));
        var segunda = await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Atendida"));

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal("Ana Pérez", (await primera.Content.ReadFromJsonAsync<AlertaInventarioDto>())!.AtendidaPor);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Visita_Completada_Descuenta_Lo_Que_Comieron_Y_Si_No_Alcanza_Abre_Inconsistencia()
    {
        _cliente.UsarToken(Token(_factory, Roles.Gerente));
        var pollo = await CrearInsumoAsync(_cliente, "Pollo", reorden: 0);
        var plato = await CrearPlatoAsync(_cliente, "Pollo asado", demanda: 100);
        (await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(1, null, [new LineaRecetaRequest(pollo.Id, 0.5m)]))).EnsureSuccessStatusCode();
        await Movimiento(_cliente, pollo.Id, "Entrada", 3);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4 personas × 0.5 kg = 2 kg: alcanza.
        await PublicarAsync(new VisitaCompletadaEvent(hoy, Guid.NewGuid(), 4, ConReserva: true));
        Assert.Equal(1, (await _cliente.GetFromJsonAsync<InsumoDto>($"/api/insumos/{pollo.Id}"))!.CantidadStock);

        // 4 personas más piden 2 kg y queda 1: el stock queda en 0 y se avisa (RN-INV-02).
        await PublicarAsync(new VisitaCompletadaEvent(hoy, Guid.NewGuid(), 4, ConReserva: false));
        Assert.Equal(0, (await _cliente.GetFromJsonAsync<InsumoDto>($"/api/insumos/{pollo.Id}"))!.CantidadStock);

        var kardex = await _cliente.GetFromJsonAsync<KardexDto>($"/api/movimientos?insumoId={pollo.Id}");
        var salidas = kardex!.Movimientos.Items.Where(m => m.Teorico).ToList();
        Assert.Equal([-2m, -1m], salidas.Select(m => m.Cantidad));
        Assert.All(salidas, m => Assert.Equal("Salida", m.Tipo));

        var alertas = await _cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas?estado=Abierta");
        Assert.Contains(alertas!.Items, a => a.Tipo == "Inconsistencia" && a.InsumoId == pollo.Id);
    }

    [Fact]
    public async Task Proyeccion_Usa_Las_Personas_Con_Reserva_Y_Las_Recetas()
    {
        var ahora = new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);
        var hoy = new DateOnly(2026, 11, 18);
        await using var factory = new GiraApiFactoryConReloj(ahora, services =>
        {
            services.RemoveAll<IConsultaReservasInventario>();
            services.AddScoped<IConsultaReservasInventario>(_ => new ReservasFalsas(new Dictionary<DateOnly, int> { [hoy.AddDays(1)] = 4 }));
        });
        using var cliente = factory.CreateClient();
        cliente.UsarToken(Token(factory, Roles.Gerente));

        var arroz = await CrearInsumoAsync(cliente, "Arroz", reorden: 5);
        var plato = await CrearPlatoAsync(cliente, "Arroz blanco", demanda: 100);
        (await cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta",
            new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 1)]))).EnsureSuccessStatusCode();
        await Movimiento(cliente, arroz.Id, "Entrada", 6);

        var proyeccion = await cliente.GetFromJsonAsync<ProyeccionConsumoDto>(
            $"/api/inventario/consumo-proyectado?desde={hoy:yyyy-MM-dd}&hasta={hoy.AddDays(2):yyyy-MM-dd}");

        Assert.Equal([0, 4, 0], proyeccion!.Dias.Select(d => d.Usados));
        var insumo = Assert.Single(proyeccion.Insumos);
        // 100 % × 1 kg / 2 porciones = 0.5 kg por persona; 4 personas = 2 kg el día siguiente: quedan 4 (< 5 de reorden).
        Assert.Equal([0m, 2m, 0m], insumo.Dias.Select(d => d.Cantidad));
        Assert.Equal(hoy.AddDays(1), insumo.FechaBajoReorden);
        Assert.Null(insumo.FechaAgotamiento);

        var alertas = await cliente.GetFromJsonAsync<PagedResult<AlertaInventarioDto>>("/api/inventario/alertas?estado=Abierta");
        var quiebre = Assert.Single(alertas!.Items);
        Assert.Equal("QuiebreProyectado", quiebre.Tipo);
        Assert.Equal(hoy.AddDays(1), quiebre.FechaQuiebreEstimada);
    }

    [Fact]
    public async Task Proyeccion_No_Pasa_De_14_Dias()
    {
        _cliente.UsarToken(Token(_factory, Roles.Supervisor));
        var hasta = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);

        var respuesta = await _cliente.GetAsync($"/api/inventario/consumo-proyectado?hasta={hasta:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private async Task PublicarAsync(INotification evento)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(evento);
    }

    private static string Token(GiraApiFactory factory, string rol, Guid? usuarioId = null)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>()
            .GenerarAccessToken(usuarioId ?? Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
    }

    // Usuario real en la base, para que el kardex y las alertas muestren su nombre ("Ana Pérez").
    private async Task<string> TokenDeUsuarioRealAsync(string rol)
    {
        var id = await _factory.CrearUsuarioAsync($"{Guid.NewGuid():N}@gira.local", "Clave#Segura1", true, rol);
        return Token(_factory, rol, id);
    }

    private static Task<HttpResponseMessage> Movimiento(HttpClient cliente, Guid insumoId, string tipo, decimal cantidad, string? motivo = null) =>
        cliente.PostAsJsonAsync("/api/movimientos", new RegistrarMovimientoRequest(insumoId, tipo, cantidad, null, motivo, null, null));

    private static async Task<InsumoDto> CrearInsumoAsync(HttpClient cliente, string nombre, decimal reorden)
    {
        var unidades = await cliente.GetFromJsonAsync<List<UnidadMedidaDto>>("/api/unidades-medida");
        var unidad = unidades!.FirstOrDefault() ?? await LeerAsync<UnidadMedidaDto>(
            await cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Kilogramo", "kg")));
        var categorias = await cliente.GetFromJsonAsync<List<CategoriaDto>>("/api/categorias-insumo");
        var categoria = categorias!.FirstOrDefault() ?? await LeerAsync<CategoriaDto>(
            await cliente.PostAsJsonAsync("/api/categorias-insumo", new GuardarCategoriaRequest("General")));

        return await LeerAsync<InsumoDto>(await cliente.PostAsJsonAsync("/api/insumos",
            new GuardarInsumoRequest(nombre, categoria.Id, unidad.Id, reorden, 10, false, null)));
    }

    private static async Task<PlatoDto> CrearPlatoAsync(HttpClient cliente, string nombre, decimal demanda)
    {
        var categoria = await LeerAsync<CategoriaDto>(
            await cliente.PostAsJsonAsync("/api/categorias-plato", new GuardarCategoriaRequest($"Categoría de {nombre}")));
        var guardado = await LeerAsync<PlatoGuardadoDto>(
            await cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest(nombre, categoria.Id, 300, demanda, true)));
        return guardado.Plato;
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed class ReservasFalsas(IReadOnlyDictionary<DateOnly, int> personas) : IConsultaReservasInventario
    {
        public Task<IReadOnlyDictionary<DateOnly, int>> PersonasConReservaPorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromResult(personas);
    }
}
