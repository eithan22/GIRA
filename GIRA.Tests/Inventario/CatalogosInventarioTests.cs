using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Inventario;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using static GIRA.Tests.Inventario.ApiInventario;

namespace GIRA.Tests.Inventario;

/// <summary>Categorías, unidades (GI-429) y proveedores (GI-481): reglas y permisos de los catálogos.</summary>
public class CatalogosInventarioTests : IAsyncLifetime
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

    [Fact]
    public async Task Unidad_Se_Crea_Se_Edita_Y_No_Repite_Nombre_Ni_Abreviatura()
    {
        var kilo = await LeerAsync<UnidadMedidaDto>(await _cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Kilogramo", "kg")));
        var litro = await LeerAsync<UnidadMedidaDto>(await _cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Litro", "l")));

        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Kilogramo", "kgs"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Kilo", "kg"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PutAsJsonAsync($"/api/unidades-medida/{litro.Id}", new GuardarUnidadMedidaRequest("Kilogramo", "l"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("", "g"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.PutAsJsonAsync($"/api/unidades-medida/{Guid.NewGuid()}", new GuardarUnidadMedidaRequest("Gramo", "g"))).StatusCode);

        var editada = await LeerAsync<UnidadMedidaDto>(await _cliente.PutAsJsonAsync($"/api/unidades-medida/{kilo.Id}", new GuardarUnidadMedidaRequest("Kilogramo neto", "kg")));
        Assert.Equal("Kilogramo neto", editada.Nombre);
        Assert.Equal(["Kilogramo neto", "Litro"], (await _cliente.GetFromJsonAsync<List<UnidadMedidaDto>>("/api/unidades-medida"))!.Select(u => u.Nombre));
    }

    [Theory]
    [InlineData("/api/categorias-insumo")]
    [InlineData("/api/categorias-plato")]
    public async Task Categoria_Se_Crea_Se_Edita_Y_No_Repite_Nombre(string ruta)
    {
        var carnes = await LeerAsync<CategoriaDto>(await _cliente.PostAsJsonAsync(ruta, new GuardarCategoriaRequest("Carnes")));

        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync(ruta, new GuardarCategoriaRequest("Carnes"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync(ruta, new GuardarCategoriaRequest(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.PutAsJsonAsync($"{ruta}/{Guid.NewGuid()}", new GuardarCategoriaRequest("Lácteos"))).StatusCode);

        var editada = await LeerAsync<CategoriaDto>(await _cliente.PutAsJsonAsync($"{ruta}/{carnes.Id}", new GuardarCategoriaRequest("Carnes rojas")));
        Assert.Equal("Carnes rojas", editada.Nombre);
    }

    [Theory]
    [InlineData("/api/unidades-medida")]
    [InlineData("/api/categorias-insumo")]
    [InlineData("/api/categorias-plato")]
    public async Task Catalogos_Piden_Token_Y_Solo_Gerente_O_Administrador_Los_Crean(string ruta)
    {
        using var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(ruta)).StatusCode);

        using var mesero = _factory.CreateClient();
        mesero.UsarToken(Token(_factory, Roles.Mesero));
        Assert.Equal(HttpStatusCode.Forbidden, (await mesero.GetAsync(ruta)).StatusCode);

        using var cocina = _factory.CreateClient();
        cocina.UsarToken(Token(_factory, Roles.Cocina));
        Assert.Equal(HttpStatusCode.OK, (await cocina.GetAsync(ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cocina.PostAsJsonAsync(ruta, new GuardarUnidadMedidaRequest("Onza", "oz"))).StatusCode);

        using var administrador = _factory.CreateClient();
        administrador.UsarToken(Token(_factory, Roles.Administrador));
        Assert.Equal(HttpStatusCode.OK, (await administrador.PostAsJsonAsync(ruta, new GuardarUnidadMedidaRequest("Onza", "oz"))).StatusCode);
    }

    [Fact]
    public async Task Proveedor_Se_Crea_Se_Edita_Y_Se_Lista()
    {
        var creado = await LeerAsync<ProveedorDto>(await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest("Distribuidora Caribe", "809-555-0101", true)));
        var editado = await LeerAsync<ProveedorDto>(await _cliente.PutAsJsonAsync($"/api/proveedores/{creado.Id}", new GuardarProveedorRequest("Distribuidora Caribe SRL", "8295550102", false)));

        Assert.Equal("Distribuidora Caribe SRL", editado.Nombre);
        Assert.False(editado.Activo);
        var lista = await _cliente.GetFromJsonAsync<List<ProveedorDto>>("/api/proveedores");
        Assert.Equal(creado.Id, Assert.Single(lista!).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest("Distribuidora Caribe SRL", "8095550103", true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.PutAsJsonAsync($"/api/proveedores/{Guid.NewGuid()}", new GuardarProveedorRequest("Otro", "8095550104", true))).StatusCode);
    }

    [Theory]
    [InlineData("", "8095550101")]
    [InlineData("La Granja", "12345")]
    [InlineData("La Granja", "7095550101")]
    public async Task Proveedor_Sin_Nombre_O_Con_Telefono_Invalido_Responde_400(string nombre, string telefono)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest(nombre, telefono, true));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task No_Se_Asigna_Un_Proveedor_Inactivo_A_Un_Insumo()
    {
        var inactivo = await LeerAsync<ProveedorDto>(await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest("Cerrado", "8095550101", false)));
        var activo = await LeerAsync<ProveedorDto>(await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest("Abierto", "8095550102", true)));

        var conInactivo = await _cliente.PostAsJsonAsync("/api/insumos", await InsumoRequestAsync(_cliente, "Harina", 1, proveedorId: inactivo.Id));
        var conActivo = await CrearInsumoAsync(_cliente, "Azúcar", 1, proveedorId: activo.Id);

        Assert.Equal(HttpStatusCode.BadRequest, conInactivo.StatusCode);
        Assert.Equal("Abierto", conActivo.Proveedor);
    }

    [Theory]
    [InlineData(Roles.Cocina)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Mesero)]
    public async Task Solo_Gerente_O_Administrador_Gestionan_Proveedores(string rol)
    {
        _cliente.UsarToken(Token(_factory, rol));

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync("/api/proveedores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync("/api/proveedores", new GuardarProveedorRequest("X", "8095550101", true))).StatusCode);
    }
}
