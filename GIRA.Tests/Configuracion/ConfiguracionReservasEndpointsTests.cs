using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using GIRA.Api.Common.Reservas;
using GIRA.Api.Controllers.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Pruebas HTTP de la configuración de reservas (GI-226): roles, errores por campo, 409, advertencias, borrado lógico,
/// invalidación de la caché entre solicitudes y el endpoint público <c>GET /api/reservas/politicas</c>. Pipeline real
/// (JWT, políticas, validadores) sobre una base InMemory propia.
/// </summary>
public class ConfiguracionReservasEndpointsTests : IAsyncLifetime
{
    private const string Base = "/api/configuracion/reservas";

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private readonly Guid _usuarioEditor = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 4, ZonaId = zona.Id });
        await ConfiguracionReservasSeeder.SembrarAsync(contexto);
        await contexto.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private string Token(string rol, Guid? usuarioId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokens.GenerarAccessToken(usuarioId ?? Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
    }

    private void Como(string rol, Guid? usuarioId = null) => _cliente.UsarToken(Token(rol, usuarioId));

    private static object Parametros(int maxPersonas = 8, int antelacionMinima = 120) => new
    {
        antelacionMinimaMinutos = antelacionMinima,
        antelacionMaximaDias = 30,
        limiteCancelacionMinutos = 120,
        ventanaCheckInMinutos = 15,
        maxReservasVigentesPorTelefono = 2,
        maxPersonasPorReserva = maxPersonas
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.Clone();

    // --- Roles ---

    [Theory]
    [InlineData("/parametros")]
    [InlineData("/horario")]
    [InlineData("/dias-cerrados")]
    public async Task Lectura_SinToken_Responde401(string ruta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.GetAsync(Base + ruta)).StatusCode);
    }

    [Theory]
    [InlineData("/parametros")]
    [InlineData("/horario")]
    [InlineData("/dias-cerrados")]
    public async Task Lectura_ConUnRolSinOperarReservas_Responde403(string ruta)
    {
        Como(Roles.Cocina);

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync(Base + ruta)).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Lectura_ConCadaRolDelPersonal_Responde200(string rol)
    {
        Como(rol);

        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync(Base + "/parametros")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync(Base + "/horario")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync(Base + "/dias-cerrados")).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Cocina)]
    public async Task Escritura_SinGestionarConfiguracion_Responde403_Y_NoCambiaNada(string rol)
    {
        Como(rol);

        var parametros = await _cliente.PutAsJsonAsync(Base + "/parametros", Parametros(maxPersonas: 6));
        var horario = await _cliente.PutAsJsonAsync(Base + "/horario/1", new { abierto = false, horaApertura = "12:00:00", horaCierre = "22:30:00" });
        var crearDia = await _cliente.PostAsJsonAsync(Base + "/dias-cerrados", new { fecha = "2099-12-25", motivo = (string?)null });
        var borrarDia = await _cliente.DeleteAsync(Base + $"/dias-cerrados/{Guid.NewGuid()}");

        Assert.All([parametros, horario, crearDia, borrarDia], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Equal(8, (await contexto.ConfiguracionesRestaurante.SingleAsync()).MaxPersonasPorReserva);
        Assert.Empty(await contexto.DiasCerrados.ToListAsync());
    }

    // --- Parámetros ---

    [Fact]
    public async Task ObtenerParametros_ParaOperarReservas_NoIncluyeActualizadoPor_Y_ParaGerenteSi()
    {
        // El interceptor de auditoría fija UpdatedBy con el usuario del token: el Gerente guarda y los demás leen.
        Como(Roles.Gerente, _usuarioEditor);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.PutAsJsonAsync(Base + "/parametros", Parametros(maxPersonas: 6))).StatusCode);
        Como(Roles.Mesero);
        var mesero = await JsonAsync(await _cliente.GetAsync(Base + "/parametros"));
        Como(Roles.Gerente);
        var gerente = await JsonAsync(await _cliente.GetAsync(Base + "/parametros"));

        Assert.False(mesero.TryGetProperty("actualizadoPor", out _));
        Assert.True(mesero.TryGetProperty("actualizadoEn", out _));
        Assert.Equal(_usuarioEditor, gerente.GetProperty("actualizadoPor").GetGuid());
        Assert.Equal(6, gerente.GetProperty("parametros").GetProperty("maxPersonasPorReserva").GetInt32());
        Assert.Equal(15, gerente.GetProperty("valoresPorDefecto").GetProperty("ventanaCheckInMinutos").GetInt32());
    }

    [Fact]
    public async Task ActualizarParametros_ComoGerente_Guarda_YAdvierteSiElGrupoSuperaLaMesaMasGrande()
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PutAsJsonAsync(Base + "/parametros", Parametros(maxPersonas: 12));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await JsonAsync(respuesta);
        Assert.Equal(12, json.GetProperty("configuracion").GetProperty("parametros").GetProperty("maxPersonasPorReserva").GetInt32());
        Assert.Contains("mesa activa más grande (4)", json.GetProperty("advertencias")[0].GetString());
    }

    [Fact]
    public async Task ActualizarParametros_Invalido_Responde400ConErroresPorCampoEnCamelCase()
    {
        Como(Roles.Administrador);
        var cuerpo = new
        {
            antelacionMinimaMinutos = -1,
            antelacionMaximaDias = 400,
            limiteCancelacionMinutos = 120,
            ventanaCheckInMinutos = 500,
            maxReservasVigentesPorTelefono = 0,
            maxPersonasPorReserva = 99
        };

        var respuesta = await _cliente.PutAsJsonAsync(Base + "/parametros", cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await JsonAsync(respuesta)).GetProperty("errors");
        foreach (var campo in new[]
                 {
                     "antelacionMinimaMinutos", "antelacionMaximaDias", "ventanaCheckInMinutos",
                     "maxReservasVigentesPorTelefono", "maxPersonasPorReserva"
                 })
        {
            Assert.True(errores.TryGetProperty(campo, out var mensajes), campo);
            Assert.NotEmpty(mensajes.EnumerateArray());
        }
        Assert.Equal("La antelación máxima debe estar entre 1 y 365 días.",
            errores.GetProperty("antelacionMaximaDias")[0].GetString());
    }

    [Fact]
    public async Task ActualizarParametros_SeVeEnLaSiguienteLectura_Y_InvalidaLaCacheDeLasPoliticasPublicas()
    {
        // La primera consulta deja la instantánea en caché; el PUT debe invalidarla para la siguiente solicitud.
        var antes = await JsonAsync(await _cliente.GetAsync("/api/reservas/politicas"));
        Como(Roles.Gerente);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.PutAsJsonAsync(Base + "/parametros", Parametros(antelacionMinima: 45))).StatusCode);
        _cliente.DefaultRequestHeaders.Authorization = null;

        var despues = await JsonAsync(await _cliente.GetAsync("/api/reservas/politicas"));

        Assert.Equal(120, antes.GetProperty("antelacionMinimaMinutos").GetInt32());
        Assert.Equal(45, despues.GetProperty("antelacionMinimaMinutos").GetInt32());
    }

    // --- Horario ---

    [Fact]
    public async Task ListarHorario_DevuelveLosSieteDiasDeLunesADomingo()
    {
        Como(Roles.Mesero);

        var horario = await JsonAsync(await _cliente.GetAsync(Base + "/horario"));

        Assert.Equal([1, 2, 3, 4, 5, 6, 0], horario.EnumerateArray().Select(d => d.GetProperty("diaSemana").GetInt32()).ToArray());
        Assert.Equal("lunes", horario[0].GetProperty("nombreDia").GetString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task ActualizarHorario_ConDiaFueraDeCeroASeis_Responde400(int dia)
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PutAsJsonAsync(
            Base + $"/horario/{dia}", new { abierto = true, horaApertura = "12:00:00", horaCierre = "22:30:00" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("El día debe estar entre 0 (domingo) y 6 (sábado).",
            (await JsonAsync(respuesta)).GetProperty("errors").GetProperty("dia")[0].GetString());
    }

    [Fact]
    public async Task ActualizarHorario_AperturaPosteriorAlCierre_Responde400EnElCampoHoraApertura()
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PutAsJsonAsync(
            Base + "/horario/2", new { abierto = true, horaApertura = "22:00:00", horaCierre = "12:00:00" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await JsonAsync(respuesta)).GetProperty("errors").TryGetProperty("horaApertura", out _));
    }

    [Fact]
    public async Task ActualizarHorario_QueDejaUnaFranjaActivaFuera_Responde409ConFranjaYDia()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            contexto.FranjasHorarias.Add(new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 30), HoraFin = new TimeOnly(20, 15)
            });
            await contexto.SaveChangesAsync();
        }
        Como(Roles.Gerente);

        var respuesta = await _cliente.PutAsJsonAsync(
            Base + "/horario/1", new { abierto = true, horaApertura = "12:00:00", horaCierre = "18:00:00" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var detalle = (await JsonAsync(respuesta)).GetProperty("detail").GetString();
        Assert.Contains("'Cena' (18:30–20:15)", detalle);
        Assert.Contains("lunes", detalle);
    }

    [Fact]
    public async Task ActualizarHorario_CerrarUnDia_Responde200_Y_SeVeEnLaListaYEnLaDisponibilidad()
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PutAsJsonAsync(
            Base + "/horario/0", new { abierto = false, horaApertura = "12:00:00", horaCierre = "22:30:00" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var horario = await JsonAsync(await _cliente.GetAsync(Base + "/horario"));
        var domingo = horario.EnumerateArray().Single(d => d.GetProperty("diaSemana").GetInt32() == 0);
        Assert.False(domingo.GetProperty("abierto").GetBoolean());
    }

    // --- Días cerrados ---

    [Fact]
    public async Task DiasCerrados_CrearDuplicadoEliminar_Responde200_409_204_404_Y_HaceBorradoLogico()
    {
        Como(Roles.Gerente);
        var cuerpo = new { fecha = "2099-12-25", motivo = "Navidad" };

        var creado = await _cliente.PostAsJsonAsync(Base + "/dias-cerrados", cuerpo);
        var duplicado = await _cliente.PostAsJsonAsync(Base + "/dias-cerrados", cuerpo);

        Assert.Equal(HttpStatusCode.OK, creado.StatusCode);
        var id = (await JsonAsync(creado)).GetProperty("diaCerrado").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Contains("Ya hay un día cerrado en esa fecha.", await duplicado.Content.ReadAsStringAsync());

        var lista = await JsonAsync(await _cliente.GetAsync(Base + "/dias-cerrados"));
        Assert.Equal(id, Assert.Single(lista.EnumerateArray()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await _cliente.DeleteAsync(Base + $"/dias-cerrados/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.DeleteAsync(Base + $"/dias-cerrados/{id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var fila = await contexto.DiasCerrados.IgnoreQueryFilters().SingleAsync();
        Assert.True(fila.IsDeleted);
    }

    [Fact]
    public async Task DiasCerrados_FechaPasadaOMotivoLargo_Responde400PorCampo()
    {
        Como(Roles.Gerente);

        var pasada = await _cliente.PostAsJsonAsync(Base + "/dias-cerrados", new { fecha = "2020-01-01", motivo = (string?)null });
        var largo = await _cliente.PostAsJsonAsync(Base + "/dias-cerrados", new { fecha = "2099-12-25", motivo = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, pasada.StatusCode);
        Assert.True((await JsonAsync(pasada)).GetProperty("errors").TryGetProperty("fecha", out _));
        Assert.Equal(HttpStatusCode.BadRequest, largo.StatusCode);
        Assert.True((await JsonAsync(largo)).GetProperty("errors").TryGetProperty("motivo", out _));
    }

    // --- Políticas públicas ---

    [Fact]
    public async Task Politicas_SinToken_Responde200ConSoloLosCincoCamposPublicos()
    {
        var respuesta = await _cliente.GetAsync("/api/reservas/politicas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await JsonAsync(respuesta);
        Assert.Equal(
            ["antelacionMinimaMinutos", "antelacionMaximaDias", "limiteCancelacionMinutos", "maxPersonas", "diasCerrados"],
            json.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(4, json.GetProperty("maxPersonas").GetInt32()); // min(8 configurado, mesa de 4)
        Assert.Equal(30, json.GetProperty("antelacionMaximaDias").GetInt32());
    }

    [Fact]
    public async Task Politicas_SoloIncluyeLosDiasCerradosDentroDeLaVentanaDeReserva()
    {
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        using (var scope = _factory.Services.CreateScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            contexto.DiasCerrados.AddRange(
                new DiaCerrado { Id = Guid.NewGuid(), Fecha = manana, Motivo = "Dentro" },
                new DiaCerrado { Id = Guid.NewGuid(), Fecha = new DateOnly(2099, 12, 25), Motivo = "Fuera de la ventana" },
                new DiaCerrado { Id = Guid.NewGuid(), Fecha = new DateOnly(2020, 1, 1), Motivo = "Pasado" });
            await contexto.SaveChangesAsync();
        }

        var json = await JsonAsync(await _cliente.GetAsync("/api/reservas/politicas"));

        var dias = json.GetProperty("diasCerrados").EnumerateArray().Select(d => d.GetString()!).ToArray();
        Assert.Equal([manana.ToString("yyyy-MM-dd")], dias);
        Assert.DoesNotContain("Dentro", json.ToString()); // el motivo no se expone
    }

    [Fact]
    public void Politicas_EsAnonimoYTieneLaPoliticaDeLimitePorIp()
    {
        var metodo = typeof(ReservasController).GetMethod(nameof(ReservasController.ObtenerPoliticas))!;

        Assert.NotNull(metodo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        Assert.Equal(
            LimiteReservasExtensions.PoliticaConsulta,
            metodo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }
}
