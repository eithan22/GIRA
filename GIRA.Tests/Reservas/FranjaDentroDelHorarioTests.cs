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
/// Una franja activa debe caber en el horario de todos los días abiertos (GI-226): al crearla, al cambiar su
/// horario y al reactivarla. El horario por defecto es de 12:00 a 22:30 todos los días.
/// </summary>
public class FranjaDentroDelHorarioTests
{
    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public ParametrosReservaFalso Parametros { get; } = new();
        public FranjaService Servicio { get; }

        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Contexto = new GiraDbContext(opciones);
            Servicio = new FranjaService(
                new FranjaGestionRepository(Contexto),
                new ReservasFuturasRepository(Contexto),
                Parametros,
                new GuardarFranjaRequestValidator(),
                new ListarSalonRequestValidator(),
                new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)));
        }

        public async Task<FranjaHoraria> SembrarAsync(string nombre, int inicio, int fin, bool activa = true)
        {
            var franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = nombre, HoraInicio = new TimeOnly(inicio, 0), HoraFin = new TimeOnly(fin, 0), Activa = activa
            };
            Contexto.FranjasHorarias.Add(franja);
            await Contexto.SaveChangesAsync();
            return franja;
        }

        public void Dispose() => Contexto.Dispose();
    }

    private static GuardarFranjaRequest Request(string nombre, int inicio, int fin) =>
        new(nombre, new TimeOnly(inicio, 0), new TimeOnly(fin, 0));

    private const string FueraDeHorario =
        "La franja (09:00–11:00) no cabe en el horario del lunes (12:00–22:30). Ajusta la franja o el horario de operación.";

    [Fact]
    public async Task Crear_DentroDelHorario_Funciona()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.CrearAsync(Request("Tarde", 14, 16), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Crear_FueraDelHorario_EsConflict_ConElPrimerDiaYSuHorario()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.CrearAsync(Request("Desayuno", 9, 11), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(FueraDeHorario, resultado.Error);
        Assert.Empty(await e.Contexto.FranjasHorarias.ToListAsync());
    }

    [Fact]
    public async Task Crear_QueTerminaDespuesDelCierre_EsConflict()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.CrearAsync(new GuardarFranjaRequest("Noche", new TimeOnly(21, 0), new TimeOnly(23, 0)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Contains("(21:00–23:00)", resultado.Error);
    }

    [Fact]
    public async Task Crear_AlLimiteExactoDelHorario_Funciona()
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.CrearAsync(new GuardarFranjaRequest("Todo", new TimeOnly(12, 0), new TimeOnly(22, 30)), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Crear_ElDiaCerradoNoCuenta_SoloLosDiasAbiertos()
    {
        using var e = new Entorno();
        e.Parametros.DiasCerrados = new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>().Where(d => d != DayOfWeek.Friday));
        e.Parametros.HoraApertura = new TimeOnly(8, 0); // solo el viernes está abierto, y desde las 8

        var resultado = await e.Servicio.CrearAsync(Request("Desayuno", 9, 11), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Crear_ElMensajeNombraElPrimerDiaAbiertoQueNoCabe()
    {
        using var e = new Entorno();
        e.Parametros.DiasCerrados = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday }; // el primero abierto es el miércoles

        var resultado = await e.Servicio.CrearAsync(Request("Desayuno", 9, 11), default);

        Assert.Contains("horario del miércoles", resultado.Error);
    }

    [Fact]
    public async Task Actualizar_ElHorarioAUnoFueraDelHorario_EsConflict_Y_NoCambiaNada()
    {
        using var e = new Entorno();
        var franja = await e.SembrarAsync("Almuerzo", 12, 14);

        var resultado = await e.Servicio.ActualizarAsync(franja.Id, Request("Almuerzo", 9, 11), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(FueraDeHorario, resultado.Error);
        Assert.Equal(new TimeOnly(12, 0), (await e.Contexto.FranjasHorarias.SingleAsync()).HoraInicio);
    }

    [Fact]
    public async Task Actualizar_SoloElNombre_NoRevalidaElHorario()
    {
        using var e = new Entorno();
        var heredada = await e.SembrarAsync("Brunch", 9, 11); // quedó fuera de un horario posterior

        var resultado = await e.Servicio.ActualizarAsync(heredada.Id, Request("Brunch tardío", 9, 11), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Actualizar_UnaFranjaInactiva_NoValidaElHorario_SeValidaAlReactivarla()
    {
        using var e = new Entorno();
        var inactiva = await e.SembrarAsync("Brunch", 12, 14, activa: false);

        var cambio = await e.Servicio.ActualizarAsync(inactiva.Id, Request("Brunch", 9, 11), default);
        var reactivar = await e.Servicio.CambiarEstadoAsync(inactiva.Id, true, default);

        Assert.True(cambio.IsSuccess);
        Assert.Equal(ErrorType.Conflict, reactivar.Type);
        Assert.Equal(FueraDeHorario, reactivar.Error);
        Assert.False((await e.Contexto.FranjasHorarias.SingleAsync()).Activa);
    }

    [Fact]
    public async Task Reactivar_UnaFranjaDentroDelHorario_Funciona_Y_Desactivar_NoValidaElHorario()
    {
        using var e = new Entorno();
        var franja = await e.SembrarAsync("Almuerzo", 12, 14, activa: false);

        var reactivar = await e.Servicio.CambiarEstadoAsync(franja.Id, true, default);
        e.Parametros.HoraApertura = new TimeOnly(18, 0); // ahora la franja queda fuera, pero desactivar siempre se puede
        var desactivar = await e.Servicio.CambiarEstadoAsync(franja.Id, false, default);

        Assert.True(reactivar.IsSuccess);
        Assert.True(desactivar.IsSuccess);
    }
}
