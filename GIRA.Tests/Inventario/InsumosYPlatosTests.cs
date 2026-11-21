using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Inventario;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using static GIRA.Tests.Inventario.ApiInventario;

namespace GIRA.Tests.Inventario;

/// <summary>Insumos (GI-434), platos y su advertencia de demanda (GI-438) y recetas (GI-442).</summary>
public class InsumosYPlatosTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

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

    // --- Insumos (RF-INV-01, RN-INV-10) ---

    [Fact]
    public async Task Insumo_Con_Nombre_Repetido_Responde_409_Y_Con_Reorden_Negativo_400()
    {
        await CrearInsumoAsync(_cliente, "Arroz", 5);

        var repetido = await _cliente.PostAsJsonAsync("/api/insumos", await InsumoRequestAsync(_cliente, "Arroz", 3));
        var negativo = await _cliente.PostAsJsonAsync("/api/insumos", await InsumoRequestAsync(_cliente, "Sal", -1));

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativo.StatusCode);
    }

    [Fact]
    public async Task Un_Mesero_Que_Crea_Un_Insumo_Recibe_403_Caso_De_Prueba_2()
    {
        var cuerpo = await InsumoRequestAsync(_cliente, "Sal", 1);
        _cliente.UsarToken(Token(_factory, Roles.Mesero));

        var respuesta = await _cliente.PostAsJsonAsync("/api/insumos", cuerpo);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Desactivar_Un_Insumo_De_Una_Receta_Activa_Responde_409_Y_Sin_Receta_Se_Desactiva()
    {
        var pollo = await CrearInsumoAsync(_cliente, "Pollo", 1);
        var sal = await CrearInsumoAsync(_cliente, "Sal", 1);
        var plato = (await CrearPlatoAsync(_cliente, "Pollo guisado", 100)).Plato;
        await GuardarRecetaAsync(_cliente, plato.Id, 4, (pollo.Id, 2));

        var usado = await _cliente.PatchAsJsonAsync($"/api/insumos/{pollo.Id}/estado", new CambiarEstadoInsumoRequest(false));
        var libre = await _cliente.PatchAsJsonAsync($"/api/insumos/{sal.Id}/estado", new CambiarEstadoInsumoRequest(false));

        Assert.Equal(HttpStatusCode.Conflict, usado.StatusCode);
        Assert.False((await LeerAsync<InsumoDto>(libre)).Activo);
    }

    // --- Platos (RF-INV-03, RN-INV-05) ---

    [Fact]
    public async Task Plato_Con_Nombre_Repetido_Responde_409_Y_Con_Datos_Invalidos_400()
    {
        var plato = await CrearPlatoAsync(_cliente, "Mofongo", 100);
        var categoria = plato.Plato.CategoriaPlatoId;

        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("Mofongo", categoria, 300, 10, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("", categoria, 300, 10, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("Sancocho", categoria, -1, 10, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("Sancocho", categoria, 300, 101, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("Sancocho", Guid.NewGuid(), 300, 10, true))).StatusCode);
    }

    [Theory]
    [InlineData(90, true)]
    [InlineData(100, false)]
    [InlineData(110, true)]
    public async Task Solo_Una_Suma_De_Demanda_Del_100_Sale_Sin_Advertencia(decimal suma, bool advierte)
    {
        await CrearPlatoAsync(_cliente, "Plato A", 50);
        var guardado = await CrearPlatoAsync(_cliente, "Plato B", suma - 50);
        var listado = await _cliente.GetFromJsonAsync<ListadoPlatosDto>("/api/platos");

        Assert.Equal(suma, guardado.SumaDemandaActivos);
        Assert.Equal(advierte, guardado.Advertencia is not null);
        Assert.Equal(advierte, listado!.Advertencia is not null);
    }

    [Fact]
    public async Task Platos_Piden_Token_Y_Solo_Gerente_O_Administrador_Los_Crean()
    {
        var categoria = (await CrearPlatoAsync(_cliente, "Mofongo", 100)).Plato.CategoriaPlatoId;
        using var anonimo = _factory.CreateClient();
        _cliente.UsarToken(Token(_factory, Roles.Cocina));

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/platos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync("/api/platos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest("Tostones", categoria, 150, 10, true))).StatusCode);
    }

    // --- Recetas (RF-INV-04, RN-INV-04) ---

    [Fact]
    public async Task Receta_Rechaza_Insumo_Inactivo_Y_Cantidad_Cero_Y_Plato_Inexistente()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        var viejo = await CrearInsumoAsync(_cliente, "Insumo viejo", 1);
        await _cliente.PatchAsJsonAsync($"/api/insumos/{viejo.Id}/estado", new CambiarEstadoInsumoRequest(false));
        var plato = (await CrearPlatoAsync(_cliente, "Arroz blanco", 100)).Plato;

        var inactivo = await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta", new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(viejo.Id, 1)]));
        var cero = await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta", new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 0)]));
        var sinPlato = await _cliente.PutAsJsonAsync($"/api/platos/{Guid.NewGuid()}/receta", new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, inactivo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cero.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sinPlato.StatusCode);
        Assert.Empty((await _cliente.GetFromJsonAsync<RecetaDto>($"/api/platos/{plato.Id}/receta"))!.Lineas);
    }

    [Fact]
    public async Task Receta_La_Lee_Cocina_Y_Solo_La_Cambia_Gerente()
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", 1);
        var plato = (await CrearPlatoAsync(_cliente, "Arroz blanco", 100)).Plato;
        await GuardarRecetaAsync(_cliente, plato.Id, 2, (arroz.Id, 1));
        _cliente.UsarToken(Token(_factory, Roles.Cocina));

        var lectura = await _cliente.GetFromJsonAsync<RecetaDto>($"/api/platos/{plato.Id}/receta");
        var cambio = await _cliente.PutAsJsonAsync($"/api/platos/{plato.Id}/receta", new GuardarRecetaRequest(2, null, [new LineaRecetaRequest(arroz.Id, 3)]));

        Assert.Equal(1, Assert.Single(lectura!.Lineas).Cantidad);
        Assert.Equal(HttpStatusCode.Forbidden, cambio.StatusCode);
    }
}
