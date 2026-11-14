using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Tests.Common;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas unitarias de <see cref="DisponibilidadService"/> (GI-229), con repositorios y reloj falsos.</summary>
public class DisponibilidadServiceTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 25); // viernes
    private static readonly FranjaHoraria Almuerzo = new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Almuerzo",
        HoraInicio = new TimeOnly(12, 0),
        HoraFin = new TimeOnly(15, 0)
    };

    private static DisponibilidadService CrearServicio(
        IReadOnlyList<Mesa> mesas,
        IReadOnlyList<FranjaHoraria> franjas,
        IReadOnlyList<Reserva> reservasVigentes,
        ParametrosReservaFalso? parametros = null,
        DateTimeOffset? ahoraUtc = null)
    {
        parametros ??= new ParametrosReservaFalso();
        // Por defecto, "ahora" es la mañana del día anterior a Fecha: suficiente antelación
        // mínima (28h) y muy por debajo de la antelación máxima (30 días), para que las
        // pruebas que no están probando ventanas de tiempo no se vean afectadas por ellas.
        var ahoraPorDefecto = Fecha.AddDays(-1).ToDateTime(new TimeOnly(8, 0)).AddHours(4); // 08:00 RD = 12:00 UTC
        var reloj = new TimeProviderFalso(ahoraUtc ?? new DateTimeOffset(ahoraPorDefecto, TimeSpan.Zero));
        var validador = new ConsultarDisponibilidadRequestValidator(parametros, reloj);

        return new DisponibilidadService(
            new MesaRepositoryFalso(mesas),
            new FranjaHorariaRepositoryFalso(franjas),
            new ReservaRepositoryFalso(reservasVigentes),
            parametros,
            validador,
            reloj);
    }

    [Fact]
    public async Task ConsultarAsync_MesaLibreQueAlcanza_LaFranjaApareceDisponible()
    {
        var mesaPequena = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var servicio = CrearServicio([mesaPequena], [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!);
        Assert.Equal(Almuerzo.Id, resultado.Value![0].Id);
        Assert.True(resultado.Value![0].Disponible);
        Assert.Null(resultado.Value![0].MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_MesaMasPequenaOcupadaPeroMesaMasGrandeLibre_SigueDisponible()
    {
        var mesaPequena = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var mesaGrande = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 6 };
        var ocupandoLaPequena = new Reserva { MesaId = mesaPequena.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha };

        var servicio = CrearServicio([mesaPequena, mesaGrande], [Almuerzo], [ocupandoLaPequena]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_TodasLasMesasAdecuadasOcupadas_LaFranjaSaleDeshabilitadaSinMesas()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ocupandola = new Reserva { MesaId = mesa.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha };

        var servicio = CrearServicio([mesa], [Almuerzo], [ocupandola]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Sin mesas disponibles para 2 personas.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_MesaInactiva_NoSeCuentaPorqueElRepositorioNoLaDevuelve()
    {
        // La mesa inactiva simplemente no aparece en ListarActivasAsync (lo filtra el repositorio real).
        var servicio = CrearServicio(Array.Empty<Mesa>(), [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Sin mesas disponibles para 2 personas.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_ZonaInactiva_SuMesaNoSeCuentaPorqueElRepositorioNoLaDevuelve()
    {
        // Mesa Y zona activas las filtra MesaRepository real; aquí simulamos que la mesa de
        // una zona inactiva simplemente no está en la lista que devuelve el repositorio.
        var servicio = CrearServicio(Array.Empty<Mesa>(), [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 1), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Sin mesas disponibles para 1 persona.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_FranjaInactiva_NoSeOfrecePorqueElRepositorioNoLaDevuelve()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var servicio = CrearServicio([mesa], Array.Empty<FranjaHoraria>(), []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_DiaCerrado_Devuelve200ConTodasLasFranjasDeshabilitadas()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var parametros = new ParametrosReservaFalso { DiasCerrados = new HashSet<DayOfWeek> { Fecha.DayOfWeek } };

        var servicio = CrearServicio([mesa], [Almuerzo], [], parametros);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("El restaurante está cerrado ese día.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_FranjaDentroDeLaAntelacionMinima_SaleDeshabilitadaConElMotivo()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        // Franja empieza a las 12:00; "ahora" local son las 11:00 del mismo día: solo 1h de
        // antelación, menos que las 2h mínimas por defecto.
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 15, 0, 0, TimeSpan.Zero); // 11:00 RD

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Requiere reservar con al menos 2 horas de antelación.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_FranjaFueraDeLaAntelacionMinima_SeIncluye()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        // "Ahora" local son las 08:00 del mismo día: 4h de antelación, más que las 2h mínimas.
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 12, 0, 0, TimeSpan.Zero); // 08:00 RD

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_AntelacionMinimaCercaDeMedianocheUtc_UsaLaHoraLocalDeSantoDomingo()
    {
        // 2026-12-25 01:30 UTC son las 2026-12-24 21:30 en Santo Domingo: todavía el día
        // anterior en hora local. La franja de Almuerzo del 25 (12:00) sigue a más de 2h.
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ahoraUtc = new DateTimeOffset(2026, 12, 25, 1, 30, 0, TimeSpan.Zero);

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!);
    }

    // --- GI-403: audiencia Personal ---

    private static DateTimeOffset LocalRd(int hora, int minuto) =>
        new(Fecha.Year, Fecha.Month, Fecha.Day, hora + 4, minuto, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConsultarAsync_Personal_Incluye_La_Franja_En_Curso_Y_A_Menos_De_2h()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var enCurso = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(13, 0));
        var aPunto = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(11, 30));

        var a = await enCurso.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);
        var b = await aPunto.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);

        Assert.Single(a.Value!);
        Assert.Single(b.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_Personal_Excluye_La_Franja_Terminada_Y_Incluye_Un_Minuto_Antes_Del_Fin()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };

        var terminada = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(15, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);
        var porTerminar = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(14, 59))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);

        Assert.Empty(terminada.Value!);
        Assert.Single(porTerminar.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_Deshabilita_La_Franja_En_Curso_Y_Las_De_Menos_De_2h()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };

        var enCurso = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(13, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);
        var aPunto = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(11, 30))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);
        var justo = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(10, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.False(Assert.Single(enCurso.Value!).Disponible);
        Assert.False(Assert.Single(aPunto.Value!).Disponible);
        Assert.Equal("Requiere reservar con al menos 2 horas de antelación.", enCurso.Value![0].MotivoNoDisponible);
        Assert.True(Assert.Single(justo.Value!).Disponible); // inicio == ahora + 2 h exactas: todavía pasa
    }

    [Fact]
    public async Task ConsultarAsync_Personal_Grupo_Mayor_A_La_Mesa_Mas_Grande_Usa_La_Voz_Del_Personal()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 8 };
        var servicio = CrearServicio([mesa], [Almuerzo], []);

        var personal = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 9), default, AudienciaReserva.Personal);
        var cliente = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 9), default);

        Assert.Equal(
            "Las mesas admiten como máximo 8 personas. Los grupos más grandes se atienden por teléfono, fuera del sistema.",
            personal.Error);
        Assert.Equal("Para grupos de más de 8 personas, llama al restaurante.", cliente.Error);
    }

    [Fact]
    public async Task ConsultarAsync_Personal_FechaPasada_ConservaElErrorDeValidacion()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ahoraUtc = new DateTimeOffset(2026, 12, 26, 12, 0, 0, TimeSpan.Zero);

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(
            new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
        Assert.Contains("La fecha no puede ser en el pasado.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_FechaPasada_YaNoEsErrorSinoFranjasQueYaPasaron()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ahoraUtc = new DateTimeOffset(2026, 12, 26, 12, 0, 0, TimeSpan.Zero);

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        var franja = Assert.Single(resultado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Esta franja ya pasó.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_FranjaDeHoyTerminada_DiceQueYaPaso_Y_UnMinutoAntesDelFinDiceAntelacion()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };

        var terminada = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(15, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);
        var porTerminar = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(14, 59))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Equal("Esta franja ya pasó.", Assert.Single(terminada.Value!).MotivoNoDisponible);
        Assert.Equal("Requiere reservar con al menos 2 horas de antelación.", Assert.Single(porTerminar.Value!).MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_Personal_FechaMasAlladeLaAntelacionMaxima_ConservaElErrorDeValidacion()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var fechaLejana = Fecha.AddDays(31); // antelación máxima por defecto: 30 días

        var servicio = CrearServicio([mesa], [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(
            new ConsultarDisponibilidadRequest(fechaLejana, 2), default, AudienciaReserva.Personal);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
        Assert.Contains("La fecha supera la ventana máxima de anticipación.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_FechaMasAlladeLaAntelacionMaxima_SaleDeshabilitadaConElMaximoReal()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var parametros = new ParametrosReservaFalso { AntelacionMaxima = TimeSpan.FromDays(10) };
        var servicio = CrearServicio([mesa], [Almuerzo], [], parametros);

        var enElLimite = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha.AddDays(-1 + 10), 2), default);
        var pasado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha.AddDays(-1 + 11), 2), default);

        Assert.True(Assert.Single(enElLimite.Value!).Disponible); // hoy + 10 días exactos todavía entra
        var franja = Assert.Single(pasado.Value!);
        Assert.False(franja.Disponible);
        Assert.Equal("Fuera del rango de reserva (máximo 10 días).", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_LaAntelacionMinimaConfiguradaVaEnElMensaje()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var servicio = CrearServicio([mesa], [Almuerzo], [],
            new ParametrosReservaFalso { AntelacionMinima = TimeSpan.FromMinutes(90) }, LocalRd(11, 0));

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Equal("Requiere reservar con al menos 90 minutos de antelación.", Assert.Single(resultado.Value!).MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_DevuelveTodasLasFranjasActivasEnOrden_ConSuMotivoCadaUna()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var tarde = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "Tarde", HoraInicio = new TimeOnly(16, 0), HoraFin = new TimeOnly(17, 45) };
        var noche = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "Noche", HoraInicio = new TimeOnly(20, 0), HoraFin = new TimeOnly(21, 45) };
        var ocupandolaTarde = new Reserva { MesaId = mesa.Id, FranjaHorariaId = tarde.Id, Fecha = Fecha };
        // 13:00: Almuerzo en curso, Tarde ocupada, Noche libre.
        var servicio = CrearServicio([mesa], [noche, tarde, Almuerzo], [ocupandolaTarde], ahoraUtc: LocalRd(13, 0));

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Equal(["Almuerzo", "Tarde", "Noche"], resultado.Value!.Select(f => f.Nombre).ToArray());
        string?[] motivosEsperados =
            ["Requiere reservar con al menos 2 horas de antelación.", "Sin mesas disponibles para 2 personas.", null];
        Assert.Equal(motivosEsperados, resultado.Value!.Select(f => f.MotivoNoDisponible).ToArray());
        Assert.Equal([false, false, true], resultado.Value!.Select(f => f.Disponible).ToArray());
    }

    [Fact]
    public async Task ConsultarAsync_Cliente_ElDiaCerradoGanaSobreLosDemasMotivos()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var parametros = new ParametrosReservaFalso { FechasCerradas = new HashSet<DateOnly> { Fecha } };
        var servicio = CrearServicio([mesa], [Almuerzo], [new Reserva { MesaId = mesa.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha }],
            parametros, LocalRd(13, 0));

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Equal("El restaurante está cerrado ese día.", Assert.Single(resultado.Value!).MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_Personal_NuncaRecibeFranjasDeshabilitadas()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var tarde = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "Tarde", HoraInicio = new TimeOnly(16, 0), HoraFin = new TimeOnly(17, 45) };
        var ocupandolaTarde = new Reserva { MesaId = mesa.Id, FranjaHorariaId = tarde.Id, Fecha = Fecha };
        var servicio = CrearServicio([mesa], [Almuerzo, tarde], [ocupandolaTarde], ahoraUtc: LocalRd(13, 0));

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default, AudienciaReserva.Personal);

        var franja = Assert.Single(resultado.Value!); // solo el Almuerzo en curso; la Tarde ocupada no se lista
        Assert.Equal("Almuerzo", franja.Nombre);
        Assert.True(franja.Disponible);
        Assert.Null(franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task ConsultarAsync_GrupoMayorQueLaMesaMasGrande_DevuelveMensajeDeLlamarAlRestaurante()
    {
        var mesaGrande = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 6 };
        var servicio = CrearServicio([mesaGrande], [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 7), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
        Assert.Contains("6 personas", resultado.Error);
        Assert.Contains("llama al restaurante", resultado.Error);
    }
}

/// <summary>
/// Parámetros con los valores por defecto (<see cref="ParametrosReservaService.ValoresPorDefecto"/>), con
/// setters para variar lo que cada prueba necesite. <c>ObtenerAsync</c> arma la instantánea en cada llamada,
/// así que un cambio entre dos llamadas se ve en la siguiente (como al invalidar la caché).
/// </summary>
class ParametrosReservaFalso : IParametrosReservaService
{
    public TimeOnly HoraApertura { get; set; } = ParametrosReservaService.ValoresPorDefecto.HoraApertura;
    public TimeOnly HoraCierre { get; set; } = ParametrosReservaService.ValoresPorDefecto.HoraCierre;
    /// <summary>Días de la semana sin servicio.</summary>
    public IReadOnlySet<DayOfWeek> DiasCerrados { get; set; } = new HashSet<DayOfWeek>();
    /// <summary>Fechas concretas cerradas.</summary>
    public IReadOnlySet<DateOnly> FechasCerradas { get; set; } = new HashSet<DateOnly>();
    public TimeSpan AntelacionMinima { get; set; } = TimeSpan.FromMinutes(ParametrosReservaService.ValoresPorDefecto.AntelacionMinimaMinutos);
    public TimeSpan AntelacionMaxima { get; set; } = TimeSpan.FromDays(ParametrosReservaService.ValoresPorDefecto.AntelacionMaximaDias);
    public TimeSpan LimiteCancelacion { get; set; } = TimeSpan.FromMinutes(ParametrosReservaService.ValoresPorDefecto.LimiteCancelacionMinutos);
    public TimeSpan VentanaCheckIn { get; set; } = TimeSpan.FromMinutes(ParametrosReservaService.ValoresPorDefecto.VentanaCheckInMinutos);
    public int MaxReservasVigentesPorTelefono { get; set; } = ParametrosReservaService.ValoresPorDefecto.MaxReservasVigentesPorTelefono;
    public int MaxPersonasPorReserva { get; set; } = ParametrosReservaService.ValoresPorDefecto.MaxPersonasPorReserva;

    public Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ParametrosReserva(
            AntelacionMinima, AntelacionMaxima, LimiteCancelacion, VentanaCheckIn,
            MaxReservasVigentesPorTelefono, MaxPersonasPorReserva,
            Enum.GetValues<DayOfWeek>().ToDictionary(
                dia => dia, dia => new HorarioDia(!DiasCerrados.Contains(dia), HoraApertura, HoraCierre)),
            FechasCerradas));

    public void Invalidar()
    {
    }
}

class MesaRepositoryFalso : IMesaRepository
{
    private readonly IReadOnlyList<Mesa> _mesas;
    public MesaRepositoryFalso(IReadOnlyList<Mesa> mesas) => _mesas = mesas;
    public Task<IReadOnlyList<Mesa>> ListarActivasAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_mesas);
}

class FranjaHorariaRepositoryFalso : IFranjaHorariaRepository
{
    private readonly IReadOnlyList<FranjaHoraria> _franjas;
    public FranjaHorariaRepositoryFalso(IReadOnlyList<FranjaHoraria> franjas) => _franjas = franjas;
    public Task<IReadOnlyList<FranjaHoraria>> ListarActivasAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_franjas);

    public Task<FranjaHoraria?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_franjas.FirstOrDefault(f => f.Id == id));
}

class ReservaRepositoryFalso : IReservaRepository
{
    private readonly IReadOnlyList<Reserva> _reservas;
    public ReservaRepositoryFalso(IReadOnlyList<Reserva> reservas) => _reservas = reservas;
    public Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Reserva>>(_reservas.Where(r => r.Fecha == fecha).ToList());

    // No los usa DisponibilidadService; se implementan solo para satisfacer la interfaz.
    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<PagedResult<ReservaListadoDto>> ListarAsync(DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<ReservaDetalleDto?> ObtenerDetallePorCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public void Descartar(Reserva reserva) => throw new NotImplementedException();
    public Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public void Agregar(Reserva reserva) => throw new NotImplementedException();
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
