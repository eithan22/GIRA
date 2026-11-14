using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Entorno = GIRA.Tests.Reservas.EntornoProceso;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del marcado automático de no-shows (GI-161, GI-189, GI-231): fin de la franja (GI-404: sin tolerancia;
/// el fin exacto ya cuenta), solo Confirmada, orden de las más antiguas primero, tope de 200 por ciclo, evento
/// una sola vez tras guardar, idempotencia y aislamiento por reserva. Repositorios reales sobre EF Core
/// InMemory y un reloj que solo avanza cuando la prueba lo ordena (hora local = UTC-4).
/// </summary>
public class ProcesoNoShowsServicioTests
{
    // "Hoy" = 1 de diciembre de 2026; las franjas de las pruebas empiezan a la hora indicada.
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static DateTimeOffset Utc(DateOnly dia, int hora, int minuto) =>
        new(dia.ToDateTime(new TimeOnly(hora, minuto)).AddHours(4), TimeSpan.Zero);

    // --- Fin de la franja (GI-404): el no-show ocurre cuando la franja termina sin check-in ---

    [Fact]
    public async Task Al_Terminar_La_Franja_Se_Marca_Y_Un_Minuto_Antes_No()
    {
        using var e = new Entorno(Utc(Hoy, 13, 59)); // franja 12:00–14:00: un minuto antes del fin
        var reserva = await e.SembrarAsync(Hoy);

        var antes = await e.Servicio.MarcarNoShowsAsync(default);
        e.Reloj.Avanzar(TimeSpan.FromMinutes(1)); // 14:00 exactas: el fin cuenta como terminada
        var enElFin = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, antes.NoAsistidas);
        Assert.Equal(1, enElFin.NoAsistidas);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(reserva.Id));
    }

    [Fact]
    public async Task Pasados_Los_15_Min_Del_Inicio_Ya_No_Se_Marca_Mientras_La_Franja_Siga()
    {
        using var e = new Entorno(Utc(Hoy, 12, 16)); // antes se marcaba aquí (a los 15 min del inicio)
        var reserva = await e.SembrarAsync(Hoy);

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, resultado.NoAsistidas);
        Assert.Equal(EstadoReserva.Confirmada, await e.EstadoDeAsync(reserva.Id));
    }

    [Fact]
    public async Task Creada_Con_La_Franja_Ya_Empezada_Se_Marca_Solo_Al_Terminar_La_Franja()
    {
        // Franja 12:00–14:00; el personal la crea a las 12:50.
        using var e = new Entorno(Utc(Hoy, 12, 51));
        var reserva = await e.SembrarAsync(Hoy, 12, creadaUtc: Utc(Hoy, 12, 50).UtcDateTime);

        var recienCreada = await e.Servicio.MarcarNoShowsAsync(default);
        e.Reloj.Avanzar(TimeSpan.FromMinutes(69)); // 14:00
        var alTerminar = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, recienCreada.NoAsistidas);
        Assert.Equal(1, alTerminar.NoAsistidas);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(reserva.Id));
    }

    [Fact]
    public async Task Durante_La_Franja_Y_Reservas_Futuras_No_Se_Tocan()
    {
        using var e = new Entorno(Utc(Hoy, 13, 0));
        var enFranja = await e.SembrarAsync(Hoy, 12);
        var masTarde = await e.SembrarAsync(Hoy, 18);
        var manana = await e.SembrarAsync(Hoy.AddDays(1), 12);

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, resultado.NoAsistidas);
        Assert.All(new[] { enFranja, masTarde, manana }, r => Assert.Equal(EstadoReserva.Confirmada, e.EstadoDeAsync(r.Id).Result));
        Assert.Empty(e.Publisher.Publicados);
    }

    [Theory]
    [InlineData(EstadoReserva.EnCurso)]
    [InlineData(EstadoReserva.Completada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.NoAsistio)]
    public async Task Solo_Se_Marcan_Las_Confirmada(EstadoReserva estado)
    {
        using var e = new Entorno(Utc(Hoy, 14, 0)); // franja terminada de sobra
        var reserva = await e.SembrarAsync(Hoy, 12, estado: estado);

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, resultado.NoAsistidas);
        Assert.Equal(estado, await e.EstadoDeAsync(reserva.Id));
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Las_Confirmada_De_Dias_Pasados_Tambien_Se_Marcan_Con_Su_Fecha_Y_Franja_Originales()
    {
        using var e = new Entorno(Utc(Hoy, 9, 0));
        var antigua = await e.SembrarAsync(Hoy.AddDays(-3), 20, personas: 4);

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(1, resultado.NoAsistidas);
        var evento = Assert.IsType<ReservaNoAsistidaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new ReservaNoAsistidaEvent(antigua.Id, Hoy.AddDays(-3), antigua.FranjaHorariaId, 4), evento);
    }

    // --- Evento ---

    [Fact]
    public async Task El_Evento_Lleva_Fecha_Franja_Y_Personas_Reservadas_Y_Se_Publica_Una_Vez()
    {
        using var e = new Entorno(Utc(Hoy, 14, 0));
        var reserva = await e.SembrarAsync(Hoy, 12, personas: 5);

        await e.Servicio.MarcarNoShowsAsync(default);

        var evento = Assert.IsType<ReservaNoAsistidaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new ReservaNoAsistidaEvent(reserva.Id, Hoy, reserva.FranjaHorariaId, 5), evento);
        Assert.Equal(
            ["ReservaId", "Fecha", "FranjaHorariaId", "Personas"],
            typeof(ReservaNoAsistidaEvent).GetProperties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task El_Evento_Se_Publica_Despues_De_Guardar()
    {
        EstadoReserva? estadoAlPublicar = null;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            estadoAlPublicar = (await otro.Reservas.SingleAsync()).Estado;
        });
        using var e = new Entorno(Utc(Hoy, 14, 0), publisher);
        entorno = e;
        await e.SembrarAsync(Hoy);

        await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(EstadoReserva.NoAsistio, estadoAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Es_Idempotente_Reejecutar_No_Duplica_El_Evento_Ni_Reprocesa()
    {
        using var e = new Entorno(Utc(Hoy, 14, 0));
        await e.SembrarAsync(Hoy);

        var primera = await e.Servicio.MarcarNoShowsAsync(default);
        var segunda = await e.Servicio.MarcarNoShowsAsync(default);
        var tercera = await e.Servicio.EjecutarAsync(default);

        Assert.Equal(1, primera.NoAsistidas);
        Assert.Equal(0, segunda.NoAsistidas);
        Assert.Equal(0, tercera.NoAsistidas);
        Assert.Single(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Sin_Reservas_No_Falla()
    {
        using var e = new Entorno(Utc(Hoy, 14, 0));

        var resultado = await e.Servicio.EjecutarAsync(default);

        Assert.Equal(0, resultado.NoAsistidas);
        Assert.Empty(resultado.Fallos);
        Assert.False(resultado.TopeAlcanzado);
    }

    // --- Orden y tope ---

    [Fact]
    public async Task Procesa_Las_Mas_Antiguas_Primero_Por_Fecha_Y_Luego_Por_Inicio_De_Franja()
    {
        using var e = new Entorno(Utc(Hoy, 23, 0));
        var hoyAlmuerzo = await e.SembrarAsync(Hoy, 12);
        var ayerCena = await e.SembrarAsync(Hoy.AddDays(-1), 18);
        var anteayerAlmuerzo = await e.SembrarAsync(Hoy.AddDays(-2), 12);
        var hoyTemprano = await e.SembrarAsync(Hoy, 9);

        await e.Servicio.MarcarNoShowsAsync(default);

        var orden = e.Publisher.Publicados.Cast<ReservaNoAsistidaEvent>().Select(x => x.ReservaId).ToList();
        Assert.Equal([anteayerAlmuerzo.Id, ayerCena.Id, hoyTemprano.Id, hoyAlmuerzo.Id], orden);
    }

    [Fact]
    public async Task El_Repositorio_Devuelve_Las_Confirmada_Ordenadas_Y_Respeta_El_Limite()
    {
        using var e = new Entorno(Utc(Hoy, 13, 0));
        var tercera = await e.SembrarAsync(Hoy, 12);
        var primera = await e.SembrarAsync(Hoy.AddDays(-5), 20);
        var segunda = await e.SembrarAsync(Hoy.AddDays(-5), 21);
        await e.SembrarAsync(Hoy.AddDays(1), 12); // mañana: fuera de la fecha máxima
        await e.SembrarAsync(Hoy.AddDays(-6), 12, estado: EstadoReserva.Cancelada); // no es Confirmada

        var todas = await new ReservaRepository(e.Contexto).ListarPorEstadoHastaAsync(EstadoReserva.Confirmada, Hoy, 10, default);
        var dos = await new ReservaRepository(e.Contexto).ListarPorEstadoHastaAsync(EstadoReserva.Confirmada, Hoy, 2, default);

        Assert.Equal([primera.Id, segunda.Id, tercera.Id], todas.Select(c => c.Reserva.Id).ToArray());
        Assert.Equal([primera.Id, segunda.Id], dos.Select(c => c.Reserva.Id).ToArray());
        Assert.Equal(new TimeOnly(20, 0), todas[0].HoraInicio);
    }

    [Fact]
    public async Task Con_Mas_De_200_Vencidas_Procesa_Las_200_Mas_Antiguas_Avisa_Y_El_Resto_Cae_En_El_Ciclo_Siguiente()
    {
        using var e = new Entorno(Utc(Hoy, 9, 0));
        var reservas = new List<Reserva>();
        for (var dias = 1; dias <= 205; dias++)
        {
            reservas.Add(await e.SembrarAsync(Hoy.AddDays(-dias), 12));
        }

        var primero = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(200, primero.NoAsistidas);
        Assert.True(primero.TopeAlcanzado);
        Assert.Equal(200, primero.Tope);
        Assert.Equal(200, e.Publisher.Publicados.Count);
        // Quedan sin procesar las 5 más recientes (ayer y los 4 días anteriores).
        var pendientes = reservas.Where(r => r.Fecha >= Hoy.AddDays(-5)).ToList();
        Assert.Equal(5, pendientes.Count);
        foreach (var pendiente in pendientes)
        {
            Assert.Equal(EstadoReserva.Confirmada, await e.EstadoDeAsync(pendiente.Id));
        }

        var segundo = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(5, segundo.NoAsistidas);
        Assert.False(segundo.TopeAlcanzado);
        Assert.Equal(205, e.Publisher.Publicados.Count);
    }

    // --- Aislamiento por reserva ---

    [Fact]
    public async Task Una_Reserva_Que_Falla_No_Detiene_A_Las_Demas_No_Publica_Y_Se_Reintenta_En_El_Ciclo_Siguiente()
    {
        ReservaRepositoryQueFallaAlGuardar? repo = null;
        using var e = new Entorno(Utc(Hoy, 23, 0), envolver: r => repo = new ReservaRepositoryQueFallaAlGuardar(r));
        var primera = await e.SembrarAsync(Hoy.AddDays(-3), 12);
        var fallida = await e.SembrarAsync(Hoy.AddDays(-2), 12);
        var tercera = await e.SembrarAsync(Hoy.AddDays(-1), 12);
        repo!.LlamadaQueFalla = 2; // el segundo guardado falla
        repo.Excepcion = new InvalidOperationException("Fallo simulado al guardar.");

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(2, resultado.NoAsistidas);
        var fallo = Assert.Single(resultado.Fallos);
        Assert.Equal(fallida.Id, fallo.ReservaId);
        Assert.IsType<InvalidOperationException>(fallo.Excepcion);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(primera.Id));
        Assert.Equal(EstadoReserva.Confirmada, await e.EstadoDeAsync(fallida.Id));
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(tercera.Id));
        // Solo dos eventos: la fallida no publica.
        Assert.Equal(
            [primera.Id, tercera.Id],
            e.Publisher.Publicados.Cast<ReservaNoAsistidaEvent>().Select(x => x.ReservaId).ToArray());

        // Ciclo siguiente: sin el fallo simulado, la reserva fallida se procesa sin arrastrar el error anterior.
        repo.LlamadaQueFalla = 0;
        var siguiente = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(1, siguiente.NoAsistidas);
        Assert.Empty(siguiente.Fallos);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(fallida.Id));
        Assert.Equal(3, e.Publisher.Publicados.Count);
    }

    [Fact]
    public async Task Si_Otra_Ejecucion_Ya_La_Proceso_Se_Omite_Sin_Evento_Y_Sin_Contarla_Como_Fallo()
    {
        ReservaRepositoryQueFallaAlGuardar? repo = null;
        using var e = new Entorno(Utc(Hoy, 23, 0), envolver: r => repo = new ReservaRepositoryQueFallaAlGuardar(r));
        await e.SembrarAsync(Hoy.AddDays(-1), 12);
        repo!.LlamadaQueFalla = 1;
        repo.Excepcion = new ReservaConflictoConcurrenciaException("Conflicto simulado (RowVersion).", new InvalidOperationException());

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(0, resultado.NoAsistidas);
        Assert.Equal(1, resultado.Omitidas);
        Assert.Empty(resultado.Fallos);
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Impide_Marcar_Ni_Detiene_Las_Demas()
    {
        var publisher = new PublisherQueFalla();
        using var e = new Entorno(Utc(Hoy, 23, 0), publisher);
        var primera = await e.SembrarAsync(Hoy.AddDays(-2), 12);
        var segunda = await e.SembrarAsync(Hoy.AddDays(-1), 12);

        var resultado = await e.Servicio.MarcarNoShowsAsync(default);

        Assert.Equal(2, resultado.NoAsistidas);
        Assert.Empty(resultado.Fallos);
        Assert.Equal(2, publisher.Intentos);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(primera.Id));
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(segunda.Id));
    }

    [Fact]
    public async Task La_Cancelacion_Se_Propaga_Y_No_Se_Cuenta_Como_Fallo()
    {
        using var e = new Entorno(Utc(Hoy, 23, 0));
        await e.SembrarAsync(Hoy.AddDays(-1), 12);
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => e.Servicio.MarcarNoShowsAsync(cancelacion.Token));
    }
}
