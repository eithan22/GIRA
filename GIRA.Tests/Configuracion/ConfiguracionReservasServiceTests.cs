using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Configuracion;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Exceptions.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Configuracion;
using GIRA.Infrastructure.Persistence.Seed;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Reglas de estado de <see cref="ConfiguracionReservasService"/> (GI-226): upsert de políticas, franjas dentro del
/// horario, advertencias por reservas existentes, duplicados, concurrencia e invalidación de la caché. Repositorio real
/// sobre EF Core InMemory; el salón y los parámetros son falsos.
/// </summary>
public class ConfiguracionReservasServiceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 12, 1, 16, 0, 0, TimeSpan.Zero); // martes 1/12/2026 12:00 local

    private sealed class SalonFalso : IConsultaSalon
    {
        public IReadOnlyList<FranjaActivaInfo> Franjas { get; set; } = [];
        public int Capacidad { get; set; } = 8;
        public int ReservasEnLaFecha { get; set; }
        public int ReservasEnElDiaDeSemana { get; set; }
        public DayOfWeek? DiaConsultado { get; private set; }

        public Task<IReadOnlyList<FranjaActivaInfo>> ListarFranjasActivasAsync(CancellationToken cancellationToken) => Task.FromResult(Franjas);
        public Task<int> CapacidadMaximaMesaActivaAsync(CancellationToken cancellationToken) => Task.FromResult(Capacidad);
        public Task<int> ContarReservasVigentesAsync(DateOnly fecha, CancellationToken cancellationToken) => Task.FromResult(ReservasEnLaFecha);

        public Task<int> ContarReservasVigentesPorDiaSemanaAsync(DayOfWeek dia, DateOnly desde, CancellationToken cancellationToken)
        {
            DiaConsultado = dia;
            return Task.FromResult(ReservasEnElDiaDeSemana);
        }
    }

    private sealed class ParametrosConContador : IParametrosReservaService
    {
        public int Invalidaciones { get; private set; }
        public Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public void Invalidar() => Invalidaciones++;
    }

    // Repositorio real con la posibilidad de simular una excepción al guardar.
    private sealed class RepositorioQueFalla : IConfiguracionRepository
    {
        private readonly IConfiguracionRepository _real;
        public Exception? AlGuardar { get; set; }
        public RepositorioQueFalla(IConfiguracionRepository real) => _real = real;

        public Task<ConfiguracionGuardada> LeerAsync(CancellationToken ct) => _real.LeerAsync(ct);
        public Task<ConfiguracionRestaurante?> BuscarConfiguracionAsync(CancellationToken ct) => _real.BuscarConfiguracionAsync(ct);
        public void AgregarConfiguracion(ConfiguracionRestaurante c) => _real.AgregarConfiguracion(c);
        public Task<HorarioOperacion?> BuscarHorarioAsync(DayOfWeek dia, CancellationToken ct) => _real.BuscarHorarioAsync(dia, ct);
        public void AgregarHorario(HorarioOperacion h) => _real.AgregarHorario(h);
        public Task<IReadOnlyList<DiaCerrado>> ListarDiasCerradosDesdeAsync(DateOnly desde, CancellationToken ct) => _real.ListarDiasCerradosDesdeAsync(desde, ct);
        public Task<bool> ExisteDiaCerradoAsync(DateOnly fecha, CancellationToken ct) => _real.ExisteDiaCerradoAsync(fecha, ct);
        public Task<DiaCerrado?> BuscarDiaCerradoAsync(Guid id, CancellationToken ct) => _real.BuscarDiaCerradoAsync(id, ct);
        public void AgregarDiaCerrado(DiaCerrado d) => _real.AgregarDiaCerrado(d);
        public void EliminarDiaCerrado(DiaCerrado d) => _real.EliminarDiaCerrado(d);
        public Task GuardarCambiosAsync(CancellationToken ct) =>
            AlGuardar is { } excepcion ? throw excepcion : _real.GuardarCambiosAsync(ct);
    }

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public RepositorioQueFalla Repositorio { get; }
        public SalonFalso Salon { get; } = new();
        public ParametrosConContador Parametros { get; } = new();
        public ConfiguracionReservasService Servicio { get; }

        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Contexto = new GiraDbContext(opciones);
            Repositorio = new RepositorioQueFalla(new ConfiguracionRepository(Contexto));
            Servicio = new ConfiguracionReservasService(Repositorio, Parametros, Salon, new TimeProviderFalso(Ahora));
        }

        public void Dispose() => Contexto.Dispose();
    }

    private static ActualizarParametrosRequest Request(int maxPersonas = 8) => new(90, 60, 45, 10, 3, maxPersonas);

    // --- Políticas ---

    [Fact]
    public async Task ObtenerParametros_SinFila_DevuelveLosValoresPorDefecto_Y_NoLosMarcaComoActualizados()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.ObtenerParametrosAsync(incluirAuditoria: true, default);

        var dto = resultado.Value!;
        Assert.Equal(dto.ValoresPorDefecto, dto.Parametros);
        Assert.Equal(new ParametrosEditablesDto(120, 30, 120, 15, 2, 8), dto.Parametros);
        Assert.Null(dto.ActualizadoEn);
        Assert.Null(dto.ActualizadoPor);
    }

    [Fact]
    public async Task ObtenerParametros_ActualizadoPor_SoloSeIncluyeConAuditoria()
    {
        using var e = new Entorno();
        var usuario = Guid.NewGuid();
        await ConfiguracionReservasSeeder.SembrarAsync(e.Contexto);
        var fila = await e.Contexto.ConfiguracionesRestaurante.SingleAsync();
        fila.UpdatedBy = usuario;
        fila.UpdatedAt = new DateTime(2026, 11, 30, 10, 0, 0, DateTimeKind.Utc);
        await e.Contexto.SaveChangesAsync();

        var sin = (await e.Servicio.ObtenerParametrosAsync(incluirAuditoria: false, default)).Value!;
        var con = (await e.Servicio.ObtenerParametrosAsync(incluirAuditoria: true, default)).Value!;

        Assert.Null(sin.ActualizadoPor);
        Assert.NotNull(sin.ActualizadoEn);
        Assert.Equal(usuario, con.ActualizadoPor);
    }

    [Fact]
    public async Task ActualizarParametros_SinFila_CreaLaFilaUnica_Guarda_E_InvalidaLaCache()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.ActualizarParametrosAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
        var fila = await e.Contexto.ConfiguracionesRestaurante.SingleAsync();
        Assert.Equal(ConfiguracionRestaurante.IdUnico, fila.Id);
        Assert.Equal((90, 60, 45, 10, 3, 8),
            (fila.AntelacionMinimaMinutos, fila.AntelacionMaximaDias, fila.LimiteCancelacionMinutos,
             fila.VentanaCheckInMinutos, fila.MaxReservasVigentesPorTelefono, fila.MaxPersonasPorReserva));
        Assert.Equal(new ParametrosEditablesDto(90, 60, 45, 10, 3, 8), resultado.Value!.Configuracion.Parametros);
        Assert.Equal(1, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task ActualizarParametros_ConFila_LaActualizaSinDuplicarla()
    {
        using var e = new Entorno();
        await ConfiguracionReservasSeeder.SembrarAsync(e.Contexto);

        await e.Servicio.ActualizarParametrosAsync(Request(maxPersonas: 6), default);

        var fila = await e.Contexto.ConfiguracionesRestaurante.SingleAsync();
        Assert.Equal(6, fila.MaxPersonasPorReserva);
        Assert.Equal(3, fila.MaxReservasVigentesPorTelefono);
    }

    [Fact]
    public async Task ActualizarParametros_MaximoMayorQueLaMesaMasGrande_GuardaYAdvierte()
    {
        using var e = new Entorno();
        e.Salon.Capacidad = 6;

        var resultado = await e.Servicio.ActualizarParametrosAsync(Request(maxPersonas: 10), default);

        Assert.True(resultado.IsSuccess);
        var advertencia = Assert.Single(resultado.Value!.Advertencias);
        Assert.Contains("10 personas", advertencia);
        Assert.Contains("(6)", advertencia);
        Assert.Equal(10, (await e.Contexto.ConfiguracionesRestaurante.SingleAsync()).MaxPersonasPorReserva);
    }

    [Fact]
    public async Task ActualizarParametros_ConMaximoDentroDeLaMesa_NoAdvierte()
    {
        using var e = new Entorno();
        e.Salon.Capacidad = 8;

        var resultado = await e.Servicio.ActualizarParametrosAsync(Request(maxPersonas: 8), default);

        Assert.Empty(resultado.Value!.Advertencias);
    }

    [Theory]
    [InlineData(typeof(ConfiguracionConflictoConcurrenciaException))]
    [InlineData(typeof(ConfiguracionDuplicadaException))]
    public async Task ActualizarParametros_ConConcurrencia_Es409ConMensajePropio_Y_NoInvalida(Type tipo)
    {
        using var e = new Entorno();
        e.Repositorio.AlGuardar = (Exception)Activator.CreateInstance(tipo, "x", new InvalidOperationException())!;

        var resultado = await e.Servicio.ActualizarParametrosAsync(Request(), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("La configuración cambió mientras la editabas; recárgala e inténtalo de nuevo.", resultado.Error);
        Assert.Equal(0, e.Parametros.Invalidaciones);
    }

    // --- Horario ---

    [Fact]
    public async Task ListarHorario_DevuelveLosSieteDiasDeLunesADomingo_ConElDefaultParaLosQueFaltan()
    {
        using var e = new Entorno();
        e.Contexto.HorariosOperacion.Add(new HorarioOperacion
        {
            Id = Guid.NewGuid(), DiaSemana = DayOfWeek.Sunday, Abierto = false,
            HoraApertura = new TimeOnly(10, 0), HoraCierre = new TimeOnly(14, 0)
        });
        await e.Contexto.SaveChangesAsync();

        var horario = (await e.Servicio.ListarHorarioAsync(default)).Value!;

        Assert.Equal([1, 2, 3, 4, 5, 6, 0], horario.Select(h => h.DiaSemana).ToArray());
        Assert.Equal("lunes", horario[0].NombreDia);
        Assert.True(horario[0].Abierto);
        Assert.Equal(new TimeOnly(22, 30), horario[0].HoraCierre);
        Assert.False(horario[6].Abierto);
        Assert.Equal(new TimeOnly(10, 0), horario[6].HoraApertura);
    }

    [Fact]
    public async Task ActualizarHorario_ConUnaFranjaActivaFueraDelHorario_EsConflict_ConFranjaYDia()
    {
        using var e = new Entorno();
        e.Salon.Franjas = [new FranjaActivaInfo(Guid.NewGuid(), "Cena", new TimeOnly(18, 30), new TimeOnly(20, 15))];

        var resultado = await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Monday, new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(18, 0)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(
            "La franja 'Cena' (18:30–20:15) quedaría fuera del horario del lunes (12:00–18:00). Ajusta o desactiva la franja primero.",
            resultado.Error);
        Assert.Empty(await e.Contexto.HorariosOperacion.ToListAsync());
        Assert.Equal(0, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task ActualizarHorario_QueNoDejaFranjasFuera_CreaElHorarioDelDia_E_InvalidaLaCache()
    {
        using var e = new Entorno();
        e.Salon.Franjas = [new FranjaActivaInfo(Guid.NewGuid(), "Almuerzo", new TimeOnly(12, 0), new TimeOnly(13, 45))];

        var resultado = await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Wednesday, new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(16, 0)), default);

        Assert.True(resultado.IsSuccess);
        var guardado = await e.Contexto.HorariosOperacion.SingleAsync();
        Assert.Equal(DayOfWeek.Wednesday, guardado.DiaSemana);
        Assert.Equal(new TimeOnly(16, 0), guardado.HoraCierre);
        Assert.Equal(3, resultado.Value!.Horario.DiaSemana);
        Assert.Empty(resultado.Value.Advertencias);
        Assert.Equal(1, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task ActualizarHorario_ConHorarioExistente_LoActualizaSinDuplicarlo()
    {
        using var e = new Entorno();
        await ConfiguracionReservasSeeder.SembrarAsync(e.Contexto);

        await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Friday, new ActualizarHorarioRequest(true, new TimeOnly(11, 0), new TimeOnly(23, 0)), default);

        Assert.Equal(7, await e.Contexto.HorariosOperacion.CountAsync());
        var viernes = await e.Contexto.HorariosOperacion.SingleAsync(h => h.DiaSemana == DayOfWeek.Friday);
        Assert.Equal(new TimeOnly(11, 0), viernes.HoraApertura);
    }

    [Fact]
    public async Task ActualizarHorario_CerrarUnDia_NoExigeQueLasFranjasCaben_Y_AvisaDeLasReservasFuturas()
    {
        using var e = new Entorno();
        e.Salon.Franjas = [new FranjaActivaInfo(Guid.NewGuid(), "Cena", new TimeOnly(18, 30), new TimeOnly(20, 15))];
        e.Salon.ReservasEnElDiaDeSemana = 3;

        var resultado = await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Monday, new ActualizarHorarioRequest(false, new TimeOnly(12, 0), new TimeOnly(18, 0)), default);

        Assert.True(resultado.IsSuccess);
        Assert.False((await e.Contexto.HorariosOperacion.SingleAsync()).Abierto);
        Assert.Equal(DayOfWeek.Monday, e.Salon.DiaConsultado);
        var advertencia = Assert.Single(resultado.Value!.Advertencias);
        Assert.Contains("3 reservas vigentes", advertencia);
        Assert.Contains("lunes", advertencia);
    }

    [Fact]
    public async Task ActualizarHorario_AbrirUnDia_NoConsultaLasReservas()
    {
        using var e = new Entorno();
        e.Salon.ReservasEnElDiaDeSemana = 5;

        var resultado = await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Monday, new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(22, 30)), default);

        Assert.Empty(resultado.Value!.Advertencias);
        Assert.Null(e.Salon.DiaConsultado);
    }

    [Fact]
    public async Task ActualizarHorario_ConConcurrencia_Es409ConMensajePropio()
    {
        using var e = new Entorno();
        e.Repositorio.AlGuardar = new ConfiguracionConflictoConcurrenciaException("x", new InvalidOperationException());

        var resultado = await e.Servicio.ActualizarHorarioAsync(
            DayOfWeek.Monday, new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(22, 30)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("La configuración cambió mientras la editabas; recárgala e inténtalo de nuevo.", resultado.Error);
    }

    // --- Días cerrados ---

    [Fact]
    public async Task CrearDiaCerrado_Guarda_InvalidaLaCache_YAvisaDeLasReservasVigentes()
    {
        using var e = new Entorno();
        e.Salon.ReservasEnLaFecha = 1;

        var resultado = await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), "  Navidad  "), default);

        Assert.True(resultado.IsSuccess);
        var guardado = await e.Contexto.DiasCerrados.SingleAsync();
        Assert.Equal("Navidad", guardado.Motivo);
        Assert.Equal(new DateOnly(2026, 12, 25), resultado.Value!.DiaCerrado.Fecha);
        Assert.Equal("Hay 1 reserva vigente ese día; no se cancelan, solo se impiden las reservas nuevas.",
            Assert.Single(resultado.Value.Advertencias));
        Assert.Equal(1, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task CrearDiaCerrado_SinReservasNiMotivo_NoAdvierteYGuardaElMotivoNulo()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), "   "), default);

        Assert.Empty(resultado.Value!.Advertencias);
        Assert.Null((await e.Contexto.DiasCerrados.SingleAsync()).Motivo);
    }

    [Fact]
    public async Task CrearDiaCerrado_Duplicado_Es409_Y_NoGuardaOtro()
    {
        using var e = new Entorno();
        await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), null), default);

        var segundo = await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), "Otro"), default);

        Assert.Equal(ErrorType.Conflict, segundo.Type);
        Assert.Equal("Ya hay un día cerrado en esa fecha.", segundo.Error);
        Assert.Equal(1, await e.Contexto.DiasCerrados.CountAsync());
    }

    [Fact]
    public async Task CrearDiaCerrado_ConUnaCarreraEnLaBase_Tambien_Es409()
    {
        using var e = new Entorno();
        e.Repositorio.AlGuardar = new ConfiguracionDuplicadaException("x", new InvalidOperationException());

        var resultado = await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), null), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("Ya hay un día cerrado en esa fecha.", resultado.Error);
        Assert.Equal(0, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task ListarDiasCerrados_SoloDeHoyEnAdelante_Ordenados()
    {
        using var e = new Entorno();
        foreach (var fecha in new[] { new DateOnly(2026, 12, 25), new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 8) })
        {
            e.Contexto.DiasCerrados.Add(new DiaCerrado { Id = Guid.NewGuid(), Fecha = fecha });
        }
        await e.Contexto.SaveChangesAsync();

        var dias = (await e.Servicio.ListarDiasCerradosAsync(default)).Value!;

        Assert.Equal(
            [new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 25)],
            dias.Select(d => d.Fecha).ToArray());
    }

    [Fact]
    public async Task EliminarDiaCerrado_LoQuitaDeLaListaConBorradoLogico_E_InvalidaLaCache()
    {
        using var e = new Entorno();
        var creado = (await e.Servicio.CrearDiaCerradoAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), null), default)).Value!;

        var resultado = await e.Servicio.EliminarDiaCerradoAsync(creado.DiaCerrado.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty((await e.Servicio.ListarDiasCerradosAsync(default)).Value!);
        // El borrado lógico lo hace el interceptor de auditoría (no está en este contexto); lo cubren las pruebas HTTP.
        Assert.Equal(2, e.Parametros.Invalidaciones);
    }

    [Fact]
    public async Task EliminarDiaCerrado_Inexistente_EsNotFound()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.EliminarDiaCerradoAsync(Guid.NewGuid(), default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
        Assert.Equal("El día cerrado no existe.", resultado.Error);
        Assert.Equal(0, e.Parametros.Invalidaciones);
    }
}
