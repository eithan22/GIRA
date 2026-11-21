namespace GIRA.Tests.Api;

public class CorsTests : IAsyncLifetime
{
    private const string Ruta = "/api/prueba-politicas/sin-proteger";

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
    public async Task OrigenPermitido_RecibeAccessControlAllowOrigin()
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, Ruta);
        peticion.Headers.Add("Origin", "http://localhost:5173");

        var respuesta = await _cliente.SendAsync(peticion);

        Assert.True(respuesta.Headers.TryGetValues("Access-Control-Allow-Origin", out var valores));
        Assert.Equal("http://localhost:5173", valores!.Single());
    }

    [Fact]
    public async Task OrigenNoPermitido_NoRecibeAccessControlAllowOrigin()
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, Ruta);
        peticion.Headers.Add("Origin", "http://evil.example");

        var respuesta = await _cliente.SendAsync(peticion);

        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
