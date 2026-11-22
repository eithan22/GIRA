using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Analitica;

public sealed class FeriadosEndpointsTests
{
    [Fact]
    public async Task Feriado_SeCreaConsultaYEliminaPorIdentificador()
    {
        await using var fabrica = new GiraApiFactory();
        using var cliente = fabrica.CreateClient();
        using (var scope = fabrica.Services.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "gerente@gira.local",
                "Gerente", [Roles.Gerente]).Token);
        }

        var fecha = TimeProvider.System.HoyLocal().AddDays(5);
        var crear = await cliente.PostAsJsonAsync("/api/feriados",
            new CrearDiaCerradoRequest(fecha, "Feriado de prueba"));
        Assert.Equal(HttpStatusCode.OK, crear.StatusCode);
        var creado = await crear.Content.ReadFromJsonAsync<DiaCerradoCreadoDto>();
        Assert.NotNull(creado);

        var feriados = await cliente.GetFromJsonAsync<List<DiaCerradoDto>>("/api/feriados");
        Assert.Contains(feriados!, f => f.Id == creado.DiaCerrado.Id);

        var eliminar = await cliente.DeleteAsync($"/api/feriados/{creado.DiaCerrado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.DeleteAsync($"/api/feriados/{creado.DiaCerrado.Id}")).StatusCode);
    }
}
