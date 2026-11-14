using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Una franja activa debe durar más que la tolerancia de no-show configurada (GI-404): si no, una reserva sin
/// check-in se marcaría No asistió cuando su franja ya terminó. Se valida al crearla, al cambiar su horario y
/// al reactivarla, igual que el encaje en el horario de operación.
/// </summary>
public class FranjaYToleranciaNoShowTests : IDisposable
{
    private readonly GiraDbContext _contexto;
    private readonly ParametrosReservaFalso _parametros = new();
    private readonly FranjaService _servicio;

    public FranjaYToleranciaNoShowTests()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _contexto = new GiraDbContext(opciones);
        _servicio = new FranjaService(
            new FranjaGestionRepository(_contexto),
            new ReservasFuturasRepository(_contexto),
            _parametros,
            new GuardarFranjaRequestValidator(),
            new ListarSalonRequestValidator(),
            new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)));
    }

    public void Dispose() => _contexto.Dispose();

    private static GuardarFranjaRequest Request(string nombre, TimeOnly inicio, TimeOnly fin) => new(nombre, inicio, fin);

    private async Task<FranjaHoraria> SembrarAsync(string nombre, TimeOnly inicio, TimeOnly fin, bool activa = true)
    {
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = nombre, HoraInicio = inicio, HoraFin = fin, Activa = activa
        };
        _contexto.FranjasHorarias.Add(franja);
        await _contexto.SaveChangesAsync();
        return franja;
    }

    [Fact]
    public async Task Crear_UnaFranjaMasCortaQueLaTolerancia_EsConflict_Y_NoSeGuarda()
    {
        var resultado = await _servicio.CrearAsync(
            Request("Café", new TimeOnly(16, 0), new TimeOnly(16, 10)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(
            "La franja 'Café' dura 10 minutos, que no es mayor que la tolerancia de no-show configurada (15 minutos).",
            resultado.Error);
        Assert.Empty(await _contexto.FranjasHorarias.ToListAsync());
    }

    [Fact]
    public async Task Crear_UnaFranjaQueDuraExactamenteLaTolerancia_TambienEsConflict()
    {
        var resultado = await _servicio.CrearAsync(
            Request("Café", new TimeOnly(16, 0), new TimeOnly(16, 15)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Contains("dura 15 minutos", resultado.Error);
    }

    [Fact]
    public async Task Crear_UnMinutoMasQueLaTolerancia_Funciona()
    {
        var resultado = await _servicio.CrearAsync(
            Request("Café", new TimeOnly(16, 0), new TimeOnly(16, 16)), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new TimeOnly(16, 16), (await _contexto.FranjasHorarias.SingleAsync()).HoraFin);
    }

    [Fact]
    public async Task Crear_ConLaToleranciaMasAlta_LoQueAntesCabiaYaNoCabe()
    {
        _parametros.ToleranciaNoShow = TimeSpan.FromMinutes(60);

        var resultado = await _servicio.CrearAsync(
            Request("Media hora", new TimeOnly(16, 0), new TimeOnly(16, 30)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Contains("(60 minutos)", resultado.Error);
    }

    [Fact]
    public async Task Crear_ConToleranciaCero_CualquierFranjaCabe()
    {
        _parametros.ToleranciaNoShow = TimeSpan.Zero;

        var resultado = await _servicio.CrearAsync(
            Request("Café", new TimeOnly(16, 0), new TimeOnly(16, 5)), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Actualizar_AcortandoUnaFranjaActivaPorDebajoDeLaTolerancia_EsConflict_Y_NoCambiaNada()
    {
        var franja = await SembrarAsync("Almuerzo", new TimeOnly(12, 0), new TimeOnly(13, 45));

        var resultado = await _servicio.ActualizarAsync(
            franja.Id, Request("Almuerzo", new TimeOnly(12, 0), new TimeOnly(12, 10)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Contains("dura 10 minutos", resultado.Error);
        Assert.Equal(new TimeOnly(13, 45), (await _contexto.FranjasHorarias.SingleAsync()).HoraFin);
    }

    [Fact]
    public async Task Actualizar_UnaFranjaInactiva_NoValidaLaTolerancia_SeValidaAlReactivarla()
    {
        var inactiva = await SembrarAsync("Café", new TimeOnly(16, 0), new TimeOnly(17, 0), activa: false);

        var cambio = await _servicio.ActualizarAsync(
            inactiva.Id, Request("Café", new TimeOnly(16, 0), new TimeOnly(16, 10)), default);
        var reactivar = await _servicio.CambiarEstadoAsync(inactiva.Id, true, default);

        Assert.True(cambio.IsSuccess);
        Assert.Equal(ErrorType.Conflict, reactivar.Type);
        Assert.Contains("dura 10 minutos", reactivar.Error);
        Assert.False((await _contexto.FranjasHorarias.SingleAsync()).Activa);
    }

    [Fact]
    public async Task Desactivar_UnaFranjaMasCortaQueLaTolerancia_SiempreSePuede()
    {
        var franja = await SembrarAsync("Café", new TimeOnly(16, 0), new TimeOnly(16, 10));

        var resultado = await _servicio.CambiarEstadoAsync(franja.Id, false, default);

        Assert.True(resultado.IsSuccess);
        Assert.False((await _contexto.FranjasHorarias.SingleAsync()).Activa);
    }
}
