using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Inventario;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static GIRA.Tests.Inventario.ApiInventario;

namespace GIRA.Tests.Inventario;

/// <summary>
/// Proyección por eventos de Reservas y Analítica (GI-458, GI-455), días configurables (GI-468) y alertas
/// (GI-469, GI-473). Receta de prueba: 100 % de los comensales piden un plato de 1 kg de arroz para 2 porciones,
/// o sea 0.5 kg por persona.
/// </summary>
public class ProyeccionYAlertasTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 11, 18);
    private readonly ReservasFalsas _reservas = new();
    private readonly AlertasPublicadas _publicadas = new();
    private readonly GiraApiFactory _factory;
    private HttpClient _cliente = null!;

    public ProyeccionYAlertasTests()
    {
        // 18/11/2026 11:00 en Santo Domingo.
        _factory = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero), services =>
        {
            services.RemoveAll<IConsultaReservasInventario>();
            services.AddSingleton<IConsultaReservasInventario>(_reservas);
            services.AddSingleton<INotificationHandler<AlertaInventarioGeneradaEvent>>(_publicadas);
        });
    }

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

    // --- Proyección (RF-INV-07, RN-INV-06) ---

    [Fact]
    public async Task Confirmar_Una_Reserva_De_4_Suma_2_Kg_Y_Cancelarla_Lo_Devuelve()
    {
        var arroz = await ArrozConRecetaAsync(stock: 50, reorden: 1);
        var fecha = Hoy.AddDays(2);
        _reservas.Personas[fecha] = 2;
        await PublicarAsync(_factory, Confirmada(fecha, 2));
        Assert.Equal(1m, await EsperadoAsync(fecha, arroz.Id));

        _reservas.Personas[fecha] = 6;
        await PublicarAsync(_factory, Confirmada(fecha, 4));
        // Fórmula: 6 personas × 100 % × 1 kg ÷ 2 porciones = 3 kg; la reserva de 4 sumó 2 kg.
        Assert.Equal(3m, await EsperadoAsync(fecha, arroz.Id));

        _reservas.Personas[fecha] = 2;
        await PublicarAsync(_factory, new ReservaCanceladaEvent(Guid.NewGuid(), fecha, Guid.NewGuid(), 4));
        Assert.Equal(1m, await EsperadoAsync(fecha, arroz.Id));
    }

    [Fact]
    public async Task Cambiar_La_Fecha_De_Una_Reserva_Recalcula_Las_Dos_Fechas()
    {
        var arroz = await ArrozConRecetaAsync(stock: 50, reorden: 1);
        var (antes, despues) = (Hoy.AddDays(1), Hoy.AddDays(3));
        _reservas.Personas[antes] = 4;
        await PublicarAsync(_factory, Confirmada(antes, 4));

        _reservas.Personas.TryRemove(antes, out _);
        _reservas.Personas[despues] = 4;
        await PublicarAsync(_factory, new ReservaModificadaEvent(Guid.NewGuid(), antes, Guid.NewGuid(), despues, Guid.NewGuid(), 4));

        Assert.Equal(0m, await EsperadoAsync(antes, arroz.Id));
        Assert.Equal(2m, await EsperadoAsync(despues, arroz.Id));
    }

    [Fact]
    public async Task Repetir_Un_Evento_No_Duplica_Las_Filas_De_La_Proyeccion()
    {
        var arroz = await ArrozConRecetaAsync(stock: 50, reorden: 1);
        var fecha = Hoy.AddDays(1);
        _reservas.Personas[fecha] = 4;
        var evento = Confirmada(fecha, 4);

        await PublicarAsync(_factory, evento);
        await PublicarAsync(_factory, evento);

        await using var scope = _factory.Services.CreateAsyncScope();
        var filas = await scope.ServiceProvider.GetRequiredService<GiraDbContext>().ConsumosProyectados
            .Where(c => c.Fecha == fecha && c.InsumoId == arroz.Id).ToListAsync();
        Assert.Equal(2m, Assert.Single(filas).CantidadEsperada);
    }

    [Fact]
    public async Task Una_Prediccion_Mayor_Que_Las_Reservas_Se_Usa_Y_Una_Menor_No()
    {
        var arroz = await ArrozConRecetaAsync(stock: 50, reorden: 1);
        var (dia1, dia2) = (Hoy.AddDays(1), Hoy.AddDays(2));
        _reservas.Personas[dia1] = 4;
        _reservas.Personas[dia2] = 10;

        // Dos franjas el día 1 (6 + 4 = 10 > 4 con reserva) y una el día 2 (6 < 10 con reserva).
        await PublicarAsync(_factory, new PrediccionAfluenciaGeneradaEvent(dia1, dia2,
            [new(dia1, Guid.NewGuid(), 6), new(dia1, Guid.NewGuid(), 4), new(dia2, Guid.NewGuid(), 6)]));

        Assert.Equal(5m, await EsperadoAsync(dia1, arroz.Id));
        Assert.Equal(5m, await EsperadoAsync(dia2, arroz.Id));
        var proyeccion = await ProyeccionAsync(dia1, dia2);
        Assert.Equal([(4, (int?)10, 10), (10, 6, 10)], proyeccion.Dias.Select(d => (d.ConReserva, d.Prediccion, d.Usados)));

        // La predicción nueva reemplaza la anterior: el día 1 ya no tiene franjas y vuelve a las reservas.
        await PublicarAsync(_factory, new PrediccionAfluenciaGeneradaEvent(dia1, dia2, [new(dia2, Guid.NewGuid(), 20)]));
        Assert.Equal(2m, await EsperadoAsync(dia1, arroz.Id));
        Assert.Equal(10m, await EsperadoAsync(dia2, arroz.Id));
    }

    [Fact]
    public async Task Sin_Prediccion_La_Proyeccion_Usa_Solo_Las_Reservas()
    {
        await ArrozConRecetaAsync(stock: 50, reorden: 1);
        _reservas.Personas[Hoy.AddDays(1)] = 4;

        var dia = (await ProyeccionAsync(Hoy.AddDays(1), Hoy.AddDays(1))).Dias.Single();

        Assert.Equal((4, (int?)null, 4), (dia.ConReserva, dia.Prediccion, dia.Usados));
    }

    // --- Alertas (RF-INV-09, RN-INV-07 a RN-INV-09) ---

    [Fact]
    public async Task Un_Quiebre_Proyectado_Abre_Una_Alerta_Publica_El_Evento_Y_No_Se_Duplica()
    {
        var arroz = await ArrozConRecetaAsync(stock: 10, reorden: 5);
        // 12 personas mañana × 0.5 kg = 6 kg: quedarían 4, debajo del punto de reorden (5).
        _reservas.Personas[Hoy.AddDays(1)] = 12;

        await PublicarAsync(_factory, Confirmada(Hoy.AddDays(1), 12));
        await PublicarAsync(_factory, Confirmada(Hoy.AddDays(1), 12));
        await ProyeccionAsync(Hoy, Hoy.AddDays(2));

        var alerta = Assert.Single(await AlertasAsync(_cliente));
        Assert.Equal(("QuiebreProyectado", arroz.Id, Hoy.AddDays(1), (decimal?)4m), (alerta.Tipo, alerta.InsumoId, alerta.FechaQuiebreEstimada, alerta.StockProyectado));
        var evento = Assert.Single(_publicadas.Eventos);
        Assert.Equal((alerta.Id, arroz.Id, TipoAlertaInventario.QuiebreProyectado, (DateOnly?)Hoy.AddDays(1)),
            (evento.AlertaId, evento.InsumoId, evento.Tipo, evento.FechaQuiebreEstimada));
    }

    [Fact]
    public async Task Una_Entrada_Cierra_La_Alerta_Abierta_Del_Insumo_Caso_De_Prueba_8()
    {
        var arroz = await ArrozConRecetaAsync(stock: 10, reorden: 5);
        _reservas.Personas[Hoy.AddDays(1)] = 12;
        await PublicarAsync(_factory, Confirmada(Hoy.AddDays(1), 12));
        var alerta = Assert.Single(await AlertasAsync(_cliente));

        // Con 3 kg más quedarían 7 después de mañana: sobre el punto de reorden.
        await Movimiento(_cliente, arroz.Id, "Entrada", 3);

        Assert.Empty(await AlertasAsync(_cliente));
        var cerrada = Assert.Single(await AlertasAsync(_cliente, "Atendida"));
        Assert.Equal((alerta.Id, "Sistema"), (cerrada.Id, cerrada.AtendidaPor));
    }

    [Fact]
    public async Task Sin_Configurar_Se_Usan_3_Dias_Y_Cambiar_Los_Dias_Cambia_Las_Alertas()
    {
        var arroz = await ArrozConRecetaAsync(stock: 10, reorden: 5);
        // El consumo cae dentro de 4 días: con 3 días de proyección no avisa; con 5 sí.
        _reservas.Personas[Hoy.AddDays(4)] = 12;

        var porDefecto = await _cliente.GetFromJsonAsync<ParametrosInventarioDto>("/api/configuracion/inventario");
        await PublicarAsync(_factory, Confirmada(Hoy.AddDays(4), 12));
        Assert.Equal(3, porDefecto!.DiasProyeccionInventario);
        Assert.Empty(await AlertasAsync(_cliente));

        var guardado = await _cliente.PutAsJsonAsync("/api/configuracion/inventario", new ActualizarParametrosInventarioRequest(5, 2));
        await PublicarAsync(_factory, Confirmada(Hoy.AddDays(4), 12));

        Assert.Equal(5, (await LeerAsync<ParametrosInventarioDto>(guardado)).DiasProyeccionInventario);
        var alerta = Assert.Single(await AlertasAsync(_cliente));
        Assert.Equal((arroz.Id, (DateOnly?)Hoy.AddDays(4)), (alerta.InsumoId, alerta.FechaQuiebreEstimada));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(15, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 31)]
    [InlineData(null, 2)]
    public async Task Dias_Fuera_De_Rango_Responden_400(int? proyeccion, int? vencimiento)
    {
        var respuesta = await _cliente.PutAsJsonAsync("/api/configuracion/inventario", new ActualizarParametrosInventarioRequest(proyeccion, vencimiento));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Supervisor, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Cocina, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Administrador, HttpStatusCode.OK, HttpStatusCode.OK)]
    public async Task Parametros_De_Inventario_Segun_El_Rol(string rol, HttpStatusCode leer, HttpStatusCode cambiar)
    {
        _cliente.UsarToken(Token(_factory, rol));

        Assert.Equal(leer, (await _cliente.GetAsync("/api/configuracion/inventario")).StatusCode);
        Assert.Equal(cambiar, (await _cliente.PutAsJsonAsync("/api/configuracion/inventario", new ActualizarParametrosInventarioRequest(4, 2))).StatusCode);
    }

    // --- Atender y descartar (RF-INV-10) ---

    [Fact]
    public async Task Atender_Una_Alerta_Guarda_Estado_Usuario_Y_Fecha()
    {
        var alerta = await AlertaDeStockBajoAsync();

        var atendida = await LeerAsync<AlertaInventarioDto>(
            await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Atendida")));

        Assert.Equal(("Atendida", "Ana Pérez"), (atendida.Estado, atendida.AtendidaPor));
        Assert.Equal(new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc), atendida.FechaAtencion);
    }

    [Fact]
    public async Task Atender_Una_Ya_Atendida_409_Inexistente_404_Y_Estado_Invalido_400()
    {
        var alerta = await AlertaDeStockBajoAsync();
        var ruta = $"/api/inventario/alertas/{alerta.Id}";

        var invalido = await _cliente.PatchAsJsonAsync(ruta, new CambiarEstadoAlertaRequest("Abierta"));
        await _cliente.PatchAsJsonAsync(ruta, new CambiarEstadoAlertaRequest("Atendida"));
        var repetida = await _cliente.PatchAsJsonAsync(ruta, new CambiarEstadoAlertaRequest("Descartada"));
        var inexistente = await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{Guid.NewGuid()}", new CambiarEstadoAlertaRequest("Atendida"));

        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Atender_Alertas_Sin_Token_401_Y_Con_Cocina_403()
    {
        var alerta = await AlertaDeStockBajoAsync();
        using var anonimo = _factory.CreateClient();
        _cliente.UsarToken(Token(_factory, Roles.Cocina));

        var sinToken = await anonimo.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Atendida"));
        var cocina = await _cliente.PatchAsJsonAsync($"/api/inventario/alertas/{alerta.Id}", new CambiarEstadoAlertaRequest("Atendida"));

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cocina.StatusCode);
    }

    private static ReservaConfirmadaEvent Confirmada(DateOnly fecha, int personas) =>
        new(Guid.NewGuid(), "ABC123", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fecha, personas, OrigenReserva.Web);

    private async Task<AlertaInventarioDto> AlertaDeStockBajoAsync()
    {
        var queso = await CrearInsumoAsync(_cliente, "Queso", 5);
        await Movimiento(_cliente, queso.Id, "Entrada", 2);
        return Assert.Single(await AlertasAsync(_cliente));
    }

    private async Task<InsumoDto> ArrozConRecetaAsync(decimal stock, decimal reorden)
    {
        var arroz = await CrearInsumoAsync(_cliente, "Arroz", reorden);
        var plato = (await CrearPlatoAsync(_cliente, "Arroz blanco", 100)).Plato;
        await GuardarRecetaAsync(_cliente, plato.Id, 2, (arroz.Id, 1));
        await Movimiento(_cliente, arroz.Id, "Entrada", stock);
        return arroz;
    }

    private async Task<decimal> EsperadoAsync(DateOnly fecha, Guid insumoId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<GiraDbContext>().ConsumosProyectados
            .SingleAsync(c => c.Fecha == fecha && c.InsumoId == insumoId)).CantidadEsperada;
    }

    private async Task<ProyeccionConsumoDto> ProyeccionAsync(DateOnly desde, DateOnly hasta) =>
        (await _cliente.GetFromJsonAsync<ProyeccionConsumoDto>($"/api/inventario/consumo-proyectado?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"))!;
}
