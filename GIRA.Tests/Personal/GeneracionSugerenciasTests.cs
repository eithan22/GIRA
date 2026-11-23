using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Events.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas de la generación de sugerencias desde la predicción de afluencia (GI-303, RF-PER-09, RN-PER-06 y RN-PER-07).
/// Franjas de prueba: Mañana 08–10 (activa), Mediodía 10–12 (activa) y Cerrada 22–23 (inactiva).
/// El evento se valida contra el contrato de Analítica: VersionContrato 1, EventoId y restaurante configurado.
/// </summary>
public class GeneracionSugerenciasTests
{
    private static readonly DateOnly Lunes = new(2026, 12, 7);
    private static readonly Guid Mesero = Guid.NewGuid();
    private static readonly Guid Manana = Guid.NewGuid();
    private static readonly Guid Mediodia = Guid.NewGuid();
    private static readonly Guid Cerrada = Guid.NewGuid();
    private static readonly Guid Empleado = Guid.NewGuid();
    private static readonly Guid Restaurante = Guid.NewGuid();

    [Fact]
    public async Task Prediccion_GeneraSugerenciaConRequeridoMayorEntreMinimoYComensales()
    {
        // Afluencia 45 con 20 comensales por empleado da 3; el mínimo es 2; el requerido es 3. Hay 0 turnos: faltan 3.
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);

        var sugerencia = Assert.Single(sugerencias.Sugerencias);
        Assert.Equal(3, sugerencia.PersonalRequerido);
        Assert.Equal(0, sugerencia.PersonalPlanificado);
        Assert.Equal(3, sugerencia.Diferencia);
        Assert.Equal(EstadoSugerencia.Pendiente, sugerencia.Estado);
    }

    [Fact]
    public async Task Prediccion_GuardaElEventoYElIdentificadorDeLaPrediccion()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);
        var evento = Evento(Lunes, Lunes, (Manana, 45));
        var prediccionId = evento.Predicciones[0].PrediccionId;

        await servicio.GenerarDesdePrediccionAsync(evento, default);

        var sugerencia = Assert.Single(sugerencias.Sugerencias);
        Assert.Equal(evento.EventoId, sugerencia.EventoId);
        Assert.Equal(prediccionId, sugerencia.PrediccionId);
    }

    [Fact]
    public async Task Prediccion_ElMinimoDelCargoGanaCuandoLaAfluenciaEsBaja()
    {
        // Afluencia 10 con 20 comensales da 1; el mínimo es 2; el requerido es 2.
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 10)), default);

        Assert.Equal(2, Assert.Single(sugerencias.Sugerencias).PersonalRequerido);
    }

    [Fact]
    public async Task Prediccion_PublicaUnaSugerenciaTurnoGeneradaPorCadaSugerenciaCreada()
    {
        // GI-349: después de guardar, se publica un evento por sugerencia nueva.
        var sugerencias = new RepositorioSugerenciasFalso();
        var publicador = new PublicadorPrueba();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20, publicador: publicador);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);

        var evento = Assert.IsType<SugerenciaTurnoGeneradaEvent>(Assert.Single(publicador.Publicados));
        Assert.Equal(Assert.Single(sugerencias.Sugerencias).Id, evento.SugerenciaId);
        Assert.Equal(3, evento.Diferencia);
    }

    [Fact]
    public async Task Prediccion_SinDiferenciaSignificativa_NoGeneraSugerencia()
    {
        // Requerido 2 y planificado 2: la diferencia es 0, por debajo del umbral de 1.
        var turnos = new RepositorioTurnosPrueba();
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", EstadoTurno.Programado));
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", EstadoTurno.Confirmado));
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20, turnos: turnos);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 10)), default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task Prediccion_SoloCuentanLosTurnosQueCubrenLaFranja()
    {
        // Un turno 09–10 no cubre la franja 08–10 completa y uno cancelado no cuenta: planificado 0 y faltan 2.
        var turnos = new RepositorioTurnosPrueba();
        turnos.Turnos.Add(Turno(Lunes, "09:00", "10:00", EstadoTurno.Programado));
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", EstadoTurno.Cancelado));
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20, turnos: turnos);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 10)), default);

        var sugerencia = Assert.Single(sugerencias.Sugerencias);
        Assert.Equal(0, sugerencia.PersonalPlanificado);
        Assert.Equal(2, sugerencia.Diferencia);
    }

    [Fact]
    public async Task NuevaPrediccion_VenceLasPendientesDeLasMismasFechas()
    {
        // RN-PER-07: la pendiente previa del mismo día pasa a Vencida y se genera una nueva.
        var sugerencias = new RepositorioSugerenciasFalso();
        var anterior = new SugerenciaTurno { Id = Guid.NewGuid(), Fecha = Lunes, FranjaHorariaId = Manana, CargoId = Mesero, Estado = EstadoSugerencia.Pendiente };
        sugerencias.Sugerencias.Add(anterior);
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);

        Assert.Equal(EstadoSugerencia.Vencida, anterior.Estado);
        Assert.Equal(2, sugerencias.Sugerencias.Count);
        Assert.Single(sugerencias.Sugerencias, s => s.Estado == EstadoSugerencia.Pendiente);
    }

    [Fact]
    public async Task NuevaPrediccion_NoVenceLasSugerenciasDeOtrasFechas()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var otraFecha = new SugerenciaTurno { Id = Guid.NewGuid(), Fecha = Lunes.AddDays(5), FranjaHorariaId = Manana, CargoId = Mesero, Estado = EstadoSugerencia.Pendiente };
        sugerencias.Sugerencias.Add(otraFecha);
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);

        Assert.Equal(EstadoSugerencia.Pendiente, otraFecha.Estado);
    }

    [Fact]
    public async Task Prediccion_FranjaInactivaOInexistente_SeIgnora()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Cerrada, 45), (Guid.NewGuid(), 45)), default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task EventoRepetido_NoGeneraSugerenciasDuplicadas()
    {
        // Mismo EventoId dos veces: la segunda no hace nada, así que la cuenta no cambia.
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);
        var evento = Evento(Lunes, Lunes, (Manana, 45));

        await servicio.GenerarDesdePrediccionAsync(evento, default);
        await servicio.GenerarDesdePrediccionAsync(evento, default);

        Assert.Single(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task EventoConOtroIdentificador_GeneraSugerenciasNuevasYVenceLasAnteriores()
    {
        // Otro EventoId: no es un duplicado, así que se procesa como una predicción nueva (RN-PER-07).
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);
        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 60)), default);

        Assert.Equal(2, sugerencias.Sugerencias.Count);
        Assert.Single(sugerencias.Sugerencias, s => s.Estado == EstadoSugerencia.Pendiente);
    }

    [Fact]
    public async Task EventoConOtraVersionDeContrato_NoHaceNada()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);
        var evento = Evento(Lunes, Lunes, (Manana, 45)) with { VersionContrato = 2 };

        await servicio.GenerarDesdePrediccionAsync(evento, default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task EventoDeOtroRestaurante_NoHaceNada()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);
        var evento = Evento(Lunes, Lunes, (Manana, 45)) with { RestauranteId = Guid.NewGuid() };

        await servicio.GenerarDesdePrediccionAsync(evento, default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task EventoSinIdentificador_NoHaceNada()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20);
        var evento = Evento(Lunes, Lunes, (Manana, 45)) with { EventoId = null };

        await servicio.GenerarDesdePrediccionAsync(evento, default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    [Fact]
    public async Task RestauranteNoConfigurado_NoAceptaEventos()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var servicio = Crear(sugerencias, minimo: 2, comensales: 20, restauranteConfigurado: false);

        await servicio.GenerarDesdePrediccionAsync(Evento(Lunes, Lunes, (Manana, 45)), default);

        Assert.Empty(sugerencias.Sugerencias);
    }

    private static GeneracionSugerenciasService Crear(
        RepositorioSugerenciasFalso sugerencias,
        int minimo,
        int comensales,
        RepositorioTurnosPrueba? turnos = null,
        PublicadorPrueba? publicador = null,
        bool restauranteConfigurado = true)
    {
        var requerimientos = new RepositorioRequerimientosFalso();
        requerimientos.Requerimientos.Add(new RequerimientoPersonal { CargoId = Mesero, MinimoPorFranja = minimo, ComensalesPorEmpleado = comensales });

        var franjas = new FranjasPrueba();
        franjas.Franjas.Add(new FranjaOperativaDto(Manana, "Mañana", new TimeOnly(8, 0), new TimeOnly(10, 0), true));
        franjas.Franjas.Add(new FranjaOperativaDto(Mediodia, "Mediodía", new TimeOnly(10, 0), new TimeOnly(12, 0), true));
        franjas.Franjas.Add(new FranjaOperativaDto(Cerrada, "Cerrada", new TimeOnly(22, 0), new TimeOnly(23, 0), false));

        return new GeneracionSugerenciasService(
            sugerencias,
            turnos ?? new RepositorioTurnosPrueba(),
            requerimientos,
            franjas,
            new RelojPrueba(new DateTimeOffset(2026, 11, 22, 14, 0, 0, TimeSpan.Zero)),
            publicador ?? new PublicadorPrueba(),
            new RestauranteConfiguradoPrueba(restauranteConfigurado ? Restaurante : null),
            NullLogger<GeneracionSugerenciasService>.Instance);
    }

    /// <summary>Evento que cumple el contrato: versión 1, EventoId y el restaurante configurado.</summary>
    private static PrediccionAfluenciaGeneradaEvent Evento(DateOnly desde, DateOnly hasta, params (Guid Franja, int Afluencia)[] predicciones) =>
        new(
            desde,
            hasta,
            predicciones.Select(p => new AfluenciaEstimadaFranja(desde, p.Franja, p.Afluencia, Guid.NewGuid(), "v1")).ToList(),
            EventoId: Guid.NewGuid(),
            VersionContrato: 1,
            RestauranteId: Restaurante);

    private static Turno Turno(DateOnly fecha, string inicio, string fin, EstadoTurno estado) =>
        new()
        {
            Id = Guid.NewGuid(),
            EmpleadoId = Empleado,
            CargoId = Mesero,
            Fecha = fecha,
            HoraInicio = TimeOnly.Parse(inicio),
            HoraFin = TimeOnly.Parse(fin),
            Estado = estado,
        };
}
