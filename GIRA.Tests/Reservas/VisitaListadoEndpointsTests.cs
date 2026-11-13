using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de GET /api/visitas (GI-402). El reloj del servicio marca las 13:00 del 1 de diciembre de 2026 en
/// Santo Domingo. Recorren el pipeline real (JWT, política OperarReservas) sobre una base InMemory propia por prueba.
/// </summary>
public class VisitaListadoEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _franjaId;
    private Guid _mesaId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Listado" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9501, Capacidad = 6, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo Listado", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.Add(franja);
        await contexto.SaveChangesAsync();
        _franjaId = franja.Id;
        _mesaId = mesa.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private string GenerarToken(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    // El interceptor de auditoría pone CreatedAt = ahora real al insertar; para fijar una hora concreta se corrige
    // después (en una modificación el interceptor solo toca UpdatedAt).
    private async Task SembrarAsync(DateOnly fecha, int personas, DateTime? creadaUtc = null, bool conMesa = true)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = _franjaId, MesaId = conMesa ? _mesaId : null,
            PersonasReales = personas
        };
        contexto.VisitasSinReserva.Add(visita);
        await contexto.SaveChangesAsync();
        if (creadaUtc is { } momento)
        {
            visita.CreatedAt = momento;
            await contexto.SaveChangesAsync();
        }
    }

    private static DateTime Utc(int hora, int minuto) => new(2026, 12, 1, hora, minuto, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await _cliente.GetAsync("/api/visitas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        var respuesta = await _cliente.GetAsync("/api/visitas");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Del_Personal_Responde_200(string rol)
    {
        await SembrarAsync(Dia, 3);
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync("/api/visitas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dia = await respuesta.Content.ReadFromJsonAsync<VisitasDelDiaDto>();
        Assert.Equal(1, dia!.TotalVisitas);
    }

    [Fact]
    public async Task Sin_Fecha_Usa_Hoy_Y_Con_Fecha_Explicita_Lista_Ese_Dia()
    {
        await SembrarAsync(Dia, 2);
        await SembrarAsync(Dia.AddDays(-3), 5);
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var hoy = await (await _cliente.GetAsync("/api/visitas")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();
        var explicita = await (await _cliente.GetAsync($"/api/visitas?fecha={Dia:yyyy-MM-dd}")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();
        var otra = await (await _cliente.GetAsync($"/api/visitas?fecha={Dia.AddDays(-3):yyyy-MM-dd}")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();

        Assert.Equal(Dia, hoy!.Fecha);
        Assert.Equal(2, hoy.TotalPersonas);
        Assert.Equal(hoy.TotalPersonas, explicita!.TotalPersonas);
        Assert.Equal(Dia.AddDays(-3), otra!.Fecha);
        Assert.Equal(5, otra.TotalPersonas);
    }

    [Fact]
    public async Task Devuelve_Total_Orden_Mas_Reciente_Primero_Y_Hora_Local()
    {
        await SembrarAsync(Dia, 2, Utc(16, 10)); // 12:10
        await SembrarAsync(Dia, 4, Utc(17, 5), conMesa: false); // 13:05
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var dia = await (await _cliente.GetAsync("/api/visitas")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();

        Assert.Equal(2, dia!.TotalVisitas);
        Assert.Equal(6, dia.TotalPersonas);
        Assert.False(dia.Truncada);
        Assert.Equal([4, 2], dia.Visitas.Select(v => v.Personas));
        Assert.Equal([new TimeOnly(13, 5), new TimeOnly(12, 10)], dia.Visitas.Select(v => v.HoraRegistro!.Value));
        Assert.Null(dia.Visitas[0].MesaNumero);
        Assert.Equal(9501, dia.Visitas[1].MesaNumero);
        Assert.All(dia.Visitas, v => Assert.Equal("Almuerzo Listado", v.FranjaNombre));
    }

    [Fact]
    public async Task Un_Dia_Sin_Visitas_Responde_200_Con_Ceros()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync("/api/visitas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dia = await respuesta.Content.ReadFromJsonAsync<VisitasDelDiaDto>();
        Assert.Equal(0, dia!.TotalVisitas);
        Assert.Equal(0, dia.TotalPersonas);
        Assert.Empty(dia.Visitas);
    }

    [Fact]
    public async Task Con_Mas_De_200_Visitas_Trae_200_Con_Los_Totales_Completos_Y_Truncada()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            for (var i = 0; i < 201; i++)
            {
                contexto.VisitasSinReserva.Add(new VisitaSinReserva
                {
                    Id = Guid.NewGuid(), Fecha = Dia, FranjaHorariaId = _franjaId, PersonasReales = 2
                });
            }
            await contexto.SaveChangesAsync();
        }
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var dia = await (await _cliente.GetAsync("/api/visitas")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();

        Assert.Equal(200, dia!.Visitas.Count);
        Assert.Equal(201, dia.TotalVisitas);
        Assert.Equal(402, dia.TotalPersonas);
        Assert.True(dia.Truncada);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2026-13-45")]
    [InlineData("01/12/2026")] // ambiguo: el binding de DateOnly lo leería como 12 de enero y devolvería 200
    [InlineData("12/01/2026")]
    [InlineData("2026-1-5")]
    public async Task Formato_De_Fecha_Que_No_Es_Aaaa_Mm_Dd_Responde_400_Con_El_Mensaje(string fecha)
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/visitas?fecha={Uri.EscapeDataString(fecha)}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("La fecha debe tener el formato aaaa-mm-dd.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Fecha_Futura_Responde_400_Con_El_Mensaje()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/visitas?fecha={Dia.AddDays(1):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("La fecha no puede ser futura.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Fecha_De_Hace_Mas_De_365_Dias_Responde_400_Con_El_Mensaje()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/visitas?fecha={Dia.AddDays(-366):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("La fecha no puede ser de hace más de 365 días.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_Respuesta_Solo_Tiene_Esos_Campos_Sin_Datos_Personales()
    {
        await SembrarAsync(Dia, 3);
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var json = await (await _cliente.GetAsync("/api/visitas")).Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        Assert.Equal(
            ["fecha", "totalVisitas", "totalPersonas", "truncada", "visitas"],
            raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["id", "franjaNombre", "mesaNumero", "personas", "horaRegistro"],
            raiz.GetProperty("visitas")[0].EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("telefono", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cliente", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regresion_El_POST_Sigue_Registrando_Y_La_Visita_Aparece_En_El_Listado()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var registro = await _cliente.PostAsJsonAsync("/api/visitas", new RegistrarVisitaRequest(_franjaId, _mesaId, 4));
        var dia = await (await _cliente.GetAsync("/api/visitas")).Content.ReadFromJsonAsync<VisitasDelDiaDto>();

        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        var creada = await registro.Content.ReadFromJsonAsync<VisitaDto>();
        Assert.Equal(Dia, creada!.Fecha);
        Assert.Equal(1, dia!.TotalVisitas);
        Assert.Equal(4, dia.TotalPersonas);
        Assert.Equal(creada.Id, Assert.Single(dia.Visitas).Id);
    }

    [Fact]
    public async Task Regresion_El_POST_Sin_Token_Sigue_Siendo_401()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/visitas", new RegistrarVisitaRequest(_franjaId, _mesaId, 2));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
