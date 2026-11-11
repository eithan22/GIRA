using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Entorno = GIRA.Tests.Reservas.EntornoProceso;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del cierre automático de visitas (GI-224, GI-239, GI-233): las reservas EnCurso pasan a
/// Completada cuando su franja termina (desde el instante exacto del fin), publican
/// <see cref="VisitaCompletadaEvent"/> con las personas reales una sola vez, son idempotentes y se aíslan
/// por reserva. Repositorios reales sobre EF Core InMemory y un reloj que solo avanza cuando la prueba lo
/// ordena (hora local = UTC-4). La franja sembrada a las 12:00 termina a las 14:00.
/// </summary>
public class CierreVisitasServicioTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static DateTimeOffset Utc(DateOnly dia, int hora, int minuto) =>
        new(dia.ToDateTime(new TimeOnly(hora, minuto)).AddHours(4), TimeSpan.Zero);

    // --- Fin de la franja ---

    [Fact]
    public async Task Justo_En_El_Fin_De_La_Franja_Se_Completa_Y_Un_Minuto_Antes_No()
    {
        using var e = new Entorno(Utc(Hoy, 13, 59)); // la franja de 12:00 termina a las 14:00
        var reserva = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);

        var antes = await e.Servicio.CompletarVencidasAsync(default);
        e.Reloj.Avanzar(TimeSpan.FromMinutes(1)); // 14:00 exactas
        var enElFin = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(0, antes.Completadas);
        Assert.Equal(1, enElFin.Completadas);
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(reserva.Id));
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Completada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.NoAsistio)]
    public async Task Solo_Se_Completan_Las_EnCurso(EstadoReserva estado)
    {
        using var e = new Entorno(Utc(Hoy, 20, 0)); // la franja ya terminó de sobra
        var reserva = await e.SembrarAsync(Hoy, 12, estado: estado);

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(0, resultado.Completadas);
        Assert.Equal(estado, await e.EstadoDeAsync(reserva.Id));
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Las_EnCurso_De_Dias_Pasados_Se_Completan_Con_Su_Fecha_Y_Franja_Originales()
    {
        using var e = new Entorno(Utc(Hoy, 9, 0));
        var antigua = await e.SembrarAsync(Hoy.AddDays(-3), 20, estado: EstadoReserva.EnCurso, personas: 4, personasReales: 5);

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(1, resultado.Completadas);
        var evento = Assert.IsType<VisitaCompletadaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new VisitaCompletadaEvent(Hoy.AddDays(-3), antigua.FranjaHorariaId, 5, ConReserva: true), evento);
    }

    // --- Evento ---

    [Fact]
    public async Task El_Evento_Lleva_Las_Personas_Reales_Y_ConReserva_Y_Se_Publica_Una_Vez()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));
        var reserva = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso, personas: 2, personasReales: 6);

        await e.Servicio.CompletarVencidasAsync(default);

        // PersonasReales del check-in (6), no las reservadas (2); con reserva.
        var evento = Assert.IsType<VisitaCompletadaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new VisitaCompletadaEvent(Hoy, reserva.FranjaHorariaId, 6, ConReserva: true), evento);
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
        using var e = new Entorno(Utc(Hoy, 15, 0), publisher);
        entorno = e;
        await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);

        await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(EstadoReserva.Completada, estadoAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    // --- Idempotencia ---

    [Fact]
    public async Task Es_Idempotente_Reejecutar_No_Vuelve_A_Completar_Ni_A_Publicar()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));
        await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);

        var primera = await e.Servicio.CompletarVencidasAsync(default);
        var segunda = await e.Servicio.CompletarVencidasAsync(default);
        var tercera = await e.Servicio.EjecutarAsync(default);

        Assert.Equal(1, primera.Completadas);
        Assert.Equal(0, segunda.Completadas);
        Assert.Equal(0, tercera.Completadas);
        Assert.Single(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Si_El_Personal_Ya_La_Completo_A_Mano_El_Proceso_No_La_Procesa_Ni_Publica_Otro_Evento()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));
        var reserva = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);
        // El personal la completó antes del ciclo (PATCH /completar): mismo efecto sobre el estado.
        var manual = await e.Contexto.Reservas.SingleAsync(r => r.Id == reserva.Id);
        manual.Completar();
        await e.Contexto.SaveChangesAsync();

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(0, resultado.Completadas);
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Sin_Reservas_No_Falla()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(0, resultado.Completadas);
        Assert.Empty(resultado.Fallos);
        Assert.False(resultado.TopeAlcanzado);
    }

    // --- Orden y tope ---

    [Fact]
    public async Task Procesa_Las_Mas_Antiguas_Primero()
    {
        using var e = new Entorno(Utc(Hoy, 23, 0));
        var hoyAlmuerzo = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);
        var ayerCena = await e.SembrarAsync(Hoy.AddDays(-1), 18, estado: EstadoReserva.EnCurso);
        var anteayerAlmuerzo = await e.SembrarAsync(Hoy.AddDays(-2), 12, estado: EstadoReserva.EnCurso);
        var hoyTemprano = await e.SembrarAsync(Hoy, 9, estado: EstadoReserva.EnCurso);

        await e.Servicio.CompletarVencidasAsync(default);

        // Un cuarto evento por reserva: el orden sale de la fecha y la hora de la franja.
        var franjas = e.Publisher.Publicados.Cast<VisitaCompletadaEvent>().Select(x => (x.Fecha, x.FranjaHorariaId)).ToList();
        Assert.Equal(
            [
                (Hoy.AddDays(-2), anteayerAlmuerzo.FranjaHorariaId), (Hoy.AddDays(-1), ayerCena.FranjaHorariaId),
                (Hoy, hoyTemprano.FranjaHorariaId), (Hoy, hoyAlmuerzo.FranjaHorariaId)
            ],
            franjas);
    }

    [Fact]
    public async Task El_Repositorio_Devuelve_Las_EnCurso_Con_Inicio_Y_Fin_De_Franja_Ordenadas_Y_Con_Limite()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));
        var tercera = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.EnCurso);
        var primera = await e.SembrarAsync(Hoy.AddDays(-5), 20, estado: EstadoReserva.EnCurso);
        await e.SembrarAsync(Hoy.AddDays(1), 12, estado: EstadoReserva.EnCurso); // mañana: fuera de la fecha máxima
        await e.SembrarAsync(Hoy.AddDays(-6), 12); // Confirmada: no es EnCurso

        var todas = await new ReservaRepository(e.Contexto)
            .ListarPorEstadoHastaAsync(EstadoReserva.EnCurso, Hoy, 10, default);
        var una = await new ReservaRepository(e.Contexto)
            .ListarPorEstadoHastaAsync(EstadoReserva.EnCurso, Hoy, 1, default);

        Assert.Equal([primera.Id, tercera.Id], todas.Select(c => c.Reserva.Id).ToArray());
        Assert.Equal([primera.Id], una.Select(c => c.Reserva.Id).ToArray());
        Assert.Equal(new TimeOnly(20, 0), todas[0].HoraInicio);
        Assert.Equal(new TimeOnly(22, 0), todas[0].HoraFin);
    }

    [Fact]
    public async Task Con_Mas_De_200_Terminadas_Procesa_Las_200_Mas_Antiguas_Avisa_Y_El_Resto_Cae_En_El_Ciclo_Siguiente()
    {
        using var e = new Entorno(Utc(Hoy, 9, 0));
        var reservas = new List<Reserva>();
        for (var dias = 1; dias <= 205; dias++)
        {
            reservas.Add(await e.SembrarAsync(Hoy.AddDays(-dias), 12, estado: EstadoReserva.EnCurso));
        }

        var primero = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(200, primero.Completadas);
        Assert.True(primero.TopeAlcanzado);
        Assert.Equal(200, e.Publisher.Publicados.Count);
        var pendientes = reservas.Where(r => r.Fecha >= Hoy.AddDays(-5)).ToList();
        foreach (var pendiente in pendientes)
        {
            Assert.Equal(EstadoReserva.EnCurso, await e.EstadoDeAsync(pendiente.Id));
        }

        var segundo = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(5, segundo.Completadas);
        Assert.False(segundo.TopeAlcanzado);
        Assert.Equal(205, e.Publisher.Publicados.Count);
    }

    // --- Aislamiento por reserva ---

    [Fact]
    public async Task Una_Reserva_Que_Falla_No_Detiene_A_Las_Demas_No_Publica_Y_Se_Reintenta_En_El_Ciclo_Siguiente()
    {
        ReservaRepositoryQueFallaAlGuardar? repo = null;
        using var e = new Entorno(Utc(Hoy, 23, 0), envolver: r => repo = new ReservaRepositoryQueFallaAlGuardar(r));
        var primera = await e.SembrarAsync(Hoy.AddDays(-3), 12, estado: EstadoReserva.EnCurso);
        var fallida = await e.SembrarAsync(Hoy.AddDays(-2), 12, estado: EstadoReserva.EnCurso);
        var tercera = await e.SembrarAsync(Hoy.AddDays(-1), 12, estado: EstadoReserva.EnCurso);
        repo!.LlamadaQueFalla = 2;
        repo.Excepcion = new InvalidOperationException("Fallo simulado al guardar.");

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(2, resultado.Completadas);
        var fallo = Assert.Single(resultado.Fallos);
        Assert.Equal(fallida.Id, fallo.ReservaId);
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(primera.Id));
        Assert.Equal(EstadoReserva.EnCurso, await e.EstadoDeAsync(fallida.Id));
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(tercera.Id));
        Assert.Equal(2, e.Publisher.Publicados.Count); // la fallida no publica

        repo.LlamadaQueFalla = 0;
        var siguiente = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(1, siguiente.Completadas);
        Assert.Empty(siguiente.Fallos);
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(fallida.Id));
        Assert.Equal(3, e.Publisher.Publicados.Count);
    }

    [Fact]
    public async Task Si_Otra_Ejecucion_O_El_Personal_La_Completa_A_La_Vez_Se_Omite_Sin_Evento_Ni_Fallo()
    {
        ReservaRepositoryQueFallaAlGuardar? repo = null;
        using var e = new Entorno(Utc(Hoy, 23, 0), envolver: r => repo = new ReservaRepositoryQueFallaAlGuardar(r));
        await e.SembrarAsync(Hoy.AddDays(-1), 12, estado: EstadoReserva.EnCurso);
        repo!.LlamadaQueFalla = 1;
        repo.Excepcion = new ReservaConflictoConcurrenciaException("Conflicto simulado (RowVersion).", new InvalidOperationException());

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(0, resultado.Completadas);
        Assert.Equal(1, resultado.Omitidas);
        Assert.Empty(resultado.Fallos);
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Impide_Completar_Ni_Detiene_Las_Demas()
    {
        var publisher = new PublisherQueFalla();
        using var e = new Entorno(Utc(Hoy, 23, 0), publisher);
        var primera = await e.SembrarAsync(Hoy.AddDays(-2), 12, estado: EstadoReserva.EnCurso);
        var segunda = await e.SembrarAsync(Hoy.AddDays(-1), 12, estado: EstadoReserva.EnCurso);

        var resultado = await e.Servicio.CompletarVencidasAsync(default);

        Assert.Equal(2, resultado.Completadas);
        Assert.Empty(resultado.Fallos);
        Assert.Equal(2, publisher.Intentos);
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(primera.Id));
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(segunda.Id));
    }

    // --- Un ciclo completo ---

    [Fact]
    public async Task Un_Ciclo_Completo_Marca_Los_No_Shows_Y_Cierra_Las_Visitas_Y_Combina_Los_Resultados()
    {
        using var e = new Entorno(Utc(Hoy, 15, 0));
        var noShow = await e.SembrarAsync(Hoy, 12, estado: EstadoReserva.Confirmada, personas: 3); // nunca llegó
        var enMesa = await e.SembrarAsync(Hoy, 9, estado: EstadoReserva.EnCurso, personas: 2, personasReales: 4); // ya terminó

        var resultado = await e.Servicio.EjecutarAsync(default);

        Assert.Equal(1, resultado.NoAsistidas);
        Assert.Equal(1, resultado.Completadas);
        Assert.Equal(0, resultado.Omitidas);
        Assert.Empty(resultado.Fallos);
        Assert.Equal(EstadoReserva.NoAsistio, await e.EstadoDeAsync(noShow.Id));
        Assert.Equal(EstadoReserva.Completada, await e.EstadoDeAsync(enMesa.Id));
        Assert.Single(e.Publisher.Publicados.OfType<ReservaNoAsistidaEvent>());
        var visita = Assert.Single(e.Publisher.Publicados.OfType<VisitaCompletadaEvent>());
        Assert.Equal(new VisitaCompletadaEvent(Hoy, enMesa.FranjaHorariaId, 4, ConReserva: true), visita);
    }

    [Fact]
    public async Task Los_Dos_Pasos_Tienen_Cada_Uno_Su_Propio_Tope_Y_El_Aviso_Se_Combina()
    {
        using var e = new Entorno(Utc(Hoy, 9, 0));
        for (var dias = 1; dias <= 200; dias++)
        {
            await e.SembrarAsync(Hoy.AddDays(-dias), 12, estado: EstadoReserva.EnCurso);
        }
        await e.SembrarAsync(Hoy.AddDays(-1), 20); // un no-show

        var resultado = await e.Servicio.EjecutarAsync(default);

        Assert.Equal(200, resultado.Completadas);
        Assert.Equal(1, resultado.NoAsistidas);
        Assert.True(resultado.TopeAlcanzado); // el paso de cierres llegó al tope
        Assert.Equal(200, resultado.Tope);
    }
}
