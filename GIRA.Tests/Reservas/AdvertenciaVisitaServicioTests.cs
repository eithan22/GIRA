using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Tests.Common;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas de <see cref="AdvertenciaVisitaService"/> (aviso de próxima reserva al registrar una visita sin
/// reserva): no bloquea, y solo avisa si la mesa tiene una reserva vigente en la franja activa
/// INMEDIATAMENTE siguiente a aquella en la que se registraría la visita (SRS v1.4 §5.2). Una reserva dos o
/// más franjas después, o en la última franja del día, no genera aviso.
/// </summary>
public class AdvertenciaVisitaServicioTests
{
    private static readonly Guid MesaId = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 12, 25);
    private static readonly FranjaHoraria Almuerzo = new() { Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new(12, 0), HoraFin = new(15, 0), Activa = true };
    // Separada de Almuerzo por una brecha de 30 min (15:00-15:30), como las franjas reales: no son contiguas.
    private static readonly FranjaHoraria Tarde = new() { Id = Guid.NewGuid(), Nombre = "Tarde", HoraInicio = new(15, 30), HoraFin = new(17, 30), Activa = true };
    private static readonly FranjaHoraria Cena = new() { Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new(18, 0), HoraFin = new(22, 30), Activa = true };

    private static AdvertenciaVisitaService CrearServicio(
        IReadOnlyList<Reserva>? reservas = null, DateTimeOffset? ahoraUtc = null)
    {
        var mesa = new Mesa { Id = MesaId, Numero = 7, Capacidad = 4, ZonaId = Guid.NewGuid(), Activa = true };
        var reloj = new TimeProviderFalso(ahoraUtc ?? new DateTimeOffset(Hoy.Year, Hoy.Month, Hoy.Day, 16, 0, 0, TimeSpan.Zero)); // 12:00 local
        return new AdvertenciaVisitaService(
            new ReservaRepositoryDeMesaFalso(reservas ?? []),
            new FranjaHorariaRepositoryFalso([Almuerzo, Tarde, Cena]),
            new MesaGestionRepositoryParaAdvertenciaFalso(mesa),
            new ConsultarAdvertenciaMesaRequestValidator(),
            reloj);
    }

    // "Ahora" en hora local (America/Santo_Domingo, UTC-4) convertido a UTC para el reloj falso.
    private static DateTimeOffset AhoraLocal(int hora, int minuto = 0) =>
        new DateTimeOffset(Hoy.Year, Hoy.Month, Hoy.Day, hora, minuto, 0, TimeSpan.Zero).AddHours(4);

    private static ConsultarAdvertenciaMesaRequest Request(Guid? franjaId = null, string? fecha = null) =>
        new() { MesaId = MesaId.ToString(), FranjaId = (franjaId ?? Almuerzo.Id).ToString(), Fecha = fecha };

    private static Reserva ReservaVigente(Guid franjaId, int personas, EstadoReserva estado = EstadoReserva.Confirmada, DateOnly? fecha = null)
    {
        var r = new Reserva { Id = Guid.NewGuid(), MesaId = MesaId, FranjaHorariaId = franjaId, Fecha = fecha ?? Hoy, Personas = personas };
        if (estado == EstadoReserva.Cancelada) r.Cancelar(null);
        if (estado == EstadoReserva.NoAsistio) r.MarcarNoAsistio();
        return r;
    }

    [Fact]
    public async Task SinReservaEnFranjaSiguiente_SinAviso()
    {
        var servicio = CrearServicio([ReservaVigente(Almuerzo.Id, 2)]); // solo en la misma franja que se consulta

        var resultado = await servicio.ConsultarAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
        Assert.Null(resultado.Value.Mensaje);
    }

    [Fact]
    public async Task ReservaEnFranjaInmediataSiguiente_Avisa()
    {
        // "Ahora" 12:00 locales; Tarde (la franja activa INMEDIATAMENTE siguiente a Almuerzo) empieza a las
        // 15:30 → 3 h 30 min después. Hay una brecha de 30 min entre Almuerzo y Tarde, como las franjas reales.
        var servicio = CrearServicio([ReservaVigente(Tarde.Id, 5)]);

        var resultado = await servicio.ConsultarAsync(Request(), default); // franja de referencia: Almuerzo

        Assert.True(resultado.IsSuccess);
        var dto = resultado.Value!;
        Assert.True(dto.HayAviso);
        Assert.Equal("Tarde", dto.FranjaNombre);
        Assert.Equal(new TimeOnly(15, 30), dto.HoraInicio);
        Assert.Equal(5, dto.Personas);
        Assert.Equal(210, dto.MinutosFaltantes);
        Assert.Contains("La mesa 7 tiene una reserva a las 15:30 (franja Tarde, 5 personas), en 3 h 30 min.", dto.Mensaje);
    }

    [Fact]
    public async Task ReservaDosFranjasDespues_NoAvisa()
    {
        // La reserva está en Cena, pero la franja INMEDIATAMENTE siguiente a Almuerzo es Tarde (sin reserva):
        // una reserva más allá de esa franja inmediata ya no genera aviso (antes de C1, sí lo hacía).
        var servicio = CrearServicio([ReservaVigente(Cena.Id, 5)]);

        var resultado = await servicio.ConsultarAsync(Request(), default); // franja de referencia: Almuerzo

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
    }

    [Fact]
    public async Task UltimaFranjaDelDia_SinAviso()
    {
        // Franja de referencia = Cena, la última franja activa del día: no hay "siguiente" que avisar.
        var servicio = CrearServicio([]);

        var resultado = await servicio.ConsultarAsync(Request(franjaId: Cena.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
    }

    [Fact]
    public async Task ReservaCanceladaONoAsistio_NoCuentan()
    {
        // Franja de referencia = Tarde, así que la inmediata siguiente es Cena.
        var servicio = CrearServicio([
            ReservaVigente(Cena.Id, 5, EstadoReserva.Cancelada),
            ReservaVigente(Cena.Id, 3, EstadoReserva.NoAsistio),
        ]);

        var resultado = await servicio.ConsultarAsync(Request(franjaId: Tarde.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
    }

    [Fact]
    public async Task ReservaEnFranjaAnterior_NoCuenta()
    {
        // Se consulta con franja de referencia = Tarde; una reserva en Almuerzo (anterior) no debe avisar.
        var servicio = CrearServicio([ReservaVigente(Almuerzo.Id, 2)]);

        var resultado = await servicio.ConsultarAsync(Request(franjaId: Tarde.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
    }

    [Fact]
    public async Task FechaDistintaDeHoy_SoloHora_SinMinutos()
    {
        var mañana = Hoy.AddDays(1);
        var servicio = CrearServicio([ReservaVigente(Tarde.Id, 6, fecha: mañana)]);

        var resultado = await servicio.ConsultarAsync(Request(fecha: mañana.ToString("yyyy-MM-dd")), default);

        Assert.True(resultado.IsSuccess);
        var dto = resultado.Value!;
        Assert.True(dto.HayAviso);
        Assert.Null(dto.MinutosFaltantes);
        Assert.DoesNotContain(" en ", dto.Mensaje);
    }

    [Theory]
    [InlineData(14, 45, 45, "45 min")]       // menos de 60 min
    [InlineData(14, 30, 60, "1 h")]          // hora exacta
    [InlineData(14, 15, 75, "1 h 15 min")]   // horas y minutos
    public async Task FormatoDelTiempoFaltante(int hora, int minuto, int minutosEsperados, string textoEsperado)
    {
        var servicio = CrearServicio([ReservaVigente(Tarde.Id, 2)], ahoraUtc: AhoraLocal(hora, minuto));

        var resultado = await servicio.ConsultarAsync(Request(), default); // franja de referencia: Almuerzo, siguiente: Tarde (15:30)

        Assert.True(resultado.IsSuccess);
        var dto = resultado.Value!;
        Assert.True(dto.HayAviso);
        Assert.Equal(minutosEsperados, dto.MinutosFaltantes);
        Assert.Contains($"en {textoEsperado}.", dto.Mensaje);
    }

    [Fact]
    public async Task FranjaSiguienteYaTerminada_NoCuenta()
    {
        // Franja de referencia = Tarde, así que la inmediata siguiente es Cena (18:00-22:30). "Ahora" son las
        // 23:00 locales: Cena ya terminó, así que no debe avisar aunque haya una reserva vigente ahí.
        var servicio = CrearServicio([ReservaVigente(Cena.Id, 4)], ahoraUtc: AhoraLocal(23, 0));

        var resultado = await servicio.ConsultarAsync(Request(franjaId: Tarde.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.HayAviso);
    }

    [Fact]
    public async Task MesaInexistente_Es404()
    {
        var reloj = new TimeProviderFalso(new DateTimeOffset(Hoy.Year, Hoy.Month, Hoy.Day, 16, 0, 0, TimeSpan.Zero));
        var servicio = new AdvertenciaVisitaService(
            new ReservaRepositoryDeMesaFalso([]),
            new FranjaHorariaRepositoryFalso([Almuerzo, Cena]),
            new MesaGestionRepositoryParaAdvertenciaFalso(null),
            new ConsultarAdvertenciaMesaRequestValidator(),
            reloj);

        var resultado = await servicio.ConsultarAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Theory]
    [InlineData(null, "FranjaId")]
    [InlineData("", "MesaId")]
    public async Task CamposObligatoriosFaltantes_Es400(string? valor, string campo)
    {
        var servicio = CrearServicio();
        var request = new ConsultarAdvertenciaMesaRequest
        {
            MesaId = campo == "MesaId" ? valor : MesaId.ToString(),
            FranjaId = campo == "FranjaId" ? valor : Almuerzo.Id.ToString(),
        };

        var resultado = await servicio.ConsultarAsync(request, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }
}

// La "C" del filtro Confirmada/EnCurso se mantiene aquí a propósito: AdvertenciaVisitaService confía en que el
// repositorio solo le devuelva vigentes (igual que ListarVigentesPorFechaAsync en producción), así que el fake
// replica ese contrato en vez de dejar pasar cualquier estado.
class ReservaRepositoryDeMesaFalso : IReservaRepository
{
    private readonly IReadOnlyList<Reserva> _reservas;
    public ReservaRepositoryDeMesaFalso(IReadOnlyList<Reserva> reservas) => _reservas = reservas;

    public Task<IReadOnlyList<Reserva>> ListarVigentesDeMesaAsync(Guid mesaId, DateOnly fecha, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Reserva>>(_reservas
            .Where(r => r.MesaId == mesaId && r.Fecha == fecha
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso))
            .ToList());

    public Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResult<ReservaListadoDto>> ListarAsync(DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<ReservaDetalleDto?> ObtenerDetallePorCodigoAsync(string codigo, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void Descartar(Reserva reserva) => throw new NotImplementedException();
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void Agregar(Reserva reserva) => throw new NotImplementedException();
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}

class MesaGestionRepositoryParaAdvertenciaFalso : IMesaGestionRepository
{
    private readonly Mesa? _mesa;
    public MesaGestionRepositoryParaAdvertenciaFalso(Mesa? mesa) => _mesa = mesa;

    public Task<Mesa?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_mesa is not null && _mesa.Id == id ? _mesa : null);

    public Task<bool> ExisteNumeroAsync(int numero, Guid? excluirId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResult<Mesa>> ListarAsync(Guid? zonaId, bool? activa, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void Agregar(Mesa mesa) => throw new NotImplementedException();
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
