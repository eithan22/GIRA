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
    public async Task ConsultarAsync_TodasLasMesasAdecuadasOcupadas_NoHayDisponibilidad()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ocupandola = new Reserva { MesaId = mesa.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha };

        var servicio = CrearServicio([mesa], [Almuerzo], [ocupandola]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_MesaInactiva_NoSeCuentaPorqueElRepositorioNoLaDevuelve()
    {
        // La mesa inactiva simplemente no aparece en ListarActivasAsync (lo filtra el repositorio real).
        var servicio = CrearServicio(Array.Empty<Mesa>(), [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_ZonaInactiva_SuMesaNoSeCuentaPorqueElRepositorioNoLaDevuelve()
    {
        // Mesa Y zona activas las filtra MesaRepository real; aquí simulamos que la mesa de
        // una zona inactiva simplemente no está en la lista que devuelve el repositorio.
        var servicio = CrearServicio(Array.Empty<Mesa>(), [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 1), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
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
    public async Task ConsultarAsync_DiaCerrado_Devuelve200ConListaVacia()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var parametros = new ParametrosReservaFalso { DiasCerrados = new HashSet<DayOfWeek> { Fecha.DayOfWeek } };

        var servicio = CrearServicio([mesa], [Almuerzo], [], parametros);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }

    [Fact]
    public async Task ConsultarAsync_FranjaDentroDeLaAntelacionMinima_SeExcluye()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        // Franja empieza a las 12:00; "ahora" local son las 11:00 del mismo día: solo 1h de
        // antelación, menos que las 2h mínimas por defecto.
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 15, 0, 0, TimeSpan.Zero); // 11:00 RD

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
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
    public async Task ConsultarAsync_Cliente_Sigue_Excluyendo_La_Franja_En_Curso_Y_Las_De_Menos_De_2h()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };

        var enCurso = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(13, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);
        var aPunto = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(11, 30))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);
        var justo = await CrearServicio([mesa], [Almuerzo], [], ahoraUtc: LocalRd(10, 0))
            .ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Empty(enCurso.Value!);
        Assert.Empty(aPunto.Value!);
        Assert.Single(justo.Value!); // inicio == ahora + 2 h exactas: todavía pasa
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
    public async Task ConsultarAsync_FechaPasada_DevuelveErrorDeValidacion()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var ahoraUtc = new DateTimeOffset(2026, 12, 26, 12, 0, 0, TimeSpan.Zero);

        var servicio = CrearServicio([mesa], [Almuerzo], [], ahoraUtc: ahoraUtc);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task ConsultarAsync_FechaMasAlladeLaAntelacionMaxima_DevuelveErrorDeValidacion()
    {
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };
        var fechaLejana = Fecha.AddDays(31); // antelación máxima por defecto: 30 días

        var servicio = CrearServicio([mesa], [Almuerzo], []);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(fechaLejana, 2), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
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

/// <summary>Reloj con valores por defecto del SRS, con setters para variar lo que cada prueba necesite.</summary>
class ParametrosReservaFalso : IParametrosReservaService
{
    public TimeOnly HoraApertura { get; set; } = new(12, 0);
    public TimeOnly HoraCierre { get; set; } = new(22, 30);
    public IReadOnlySet<DayOfWeek> DiasCerrados { get; set; } = new HashSet<DayOfWeek>();
    public TimeSpan AntelacionMinima { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan AntelacionMaxima { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan LimiteCancelacion { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan VentanaCheckIn { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan ToleranciaNoShow { get; set; } = TimeSpan.FromMinutes(15);
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
