using System.Net;

namespace GIRA.Tests.Api;

public class ManejadorExcepcionesIntegrationTests : IAsyncLifetime
{
    private readonly GiraApiFactoryConPruebas _factory = new();
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

    [Fact]
    public async Task ExcepcionNoControlada_Devuelve500ConProblemDetailsGenerico()
    {
        var respuesta = await _cliente.GetAsync("/api/prueba-politicas/lanza-excepcion");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("mensaje-interno-de-prueba-nunca-debe-salir-al-cliente", cuerpo);
    }
}
