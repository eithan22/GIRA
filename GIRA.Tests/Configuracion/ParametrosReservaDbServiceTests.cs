using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Services.Configuracion;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Configuracion;
using Microsoft.Extensions.Caching.Memory;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Pruebas de <see cref="ParametrosReservaDbService"/> (GI-226): arma la instantánea con lo guardado, completa lo
/// que falte con los valores por defecto y guarda la instantánea en caché hasta que se invalida.
/// </summary>
public class ParametrosReservaDbServiceTests
{
    private sealed class RepositorioFalso : IConfiguracionRepository
    {
        public ConfiguracionGuardada Guardada { get; set; } = new(null, [], []);
        public int Lecturas { get; private set; }

        public Task<ConfiguracionGuardada> LeerAsync(CancellationToken cancellationToken)
        {
            Lecturas++;
            return Task.FromResult(Guardada);
        }

        // Escrituras: no las usa este servicio de solo lectura.
        public Task<ConfiguracionRestaurante?> BuscarConfiguracionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AgregarConfiguracion(ConfiguracionRestaurante configuracion) => throw new NotSupportedException();
        public Task<HorarioOperacion?> BuscarHorarioAsync(DayOfWeek dia, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AgregarHorario(HorarioOperacion horario) => throw new NotSupportedException();
        public Task<IReadOnlyList<DiaCerrado>> ListarDiasCerradosDesdeAsync(DateOnly desde, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExisteDiaCerradoAsync(DateOnly fecha, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DiaCerrado?> BuscarDiaCerradoAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AgregarDiaCerrado(DiaCerrado diaCerrado) => throw new NotSupportedException();
        public void EliminarDiaCerrado(DiaCerrado diaCerrado) => throw new NotSupportedException();
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (ParametrosReservaDbService Servicio, RepositorioFalso Repositorio) Crear()
    {
        var repositorio = new RepositorioFalso();
        return (new ParametrosReservaDbService(repositorio, new MemoryCache(new MemoryCacheOptions())), repositorio);
    }

    private static ConfiguracionRestaurante ConfiguracionDePrueba() => new()
    {
        Id = ConfiguracionRestaurante.IdUnico,
        AntelacionMinimaMinutos = 60,
        AntelacionMaximaDias = 90,
        LimiteCancelacionMinutos = 30,
        VentanaCheckInMinutos = 45,
        MaxReservasVigentesPorTelefono = 5,
        MaxPersonasPorReserva = 20
    };

    [Fact]
    public async Task ObtenerAsync_SinConfiguracionGuardada_DevuelveLosValoresPorDefecto()
    {
        var (servicio, _) = Crear();

        var parametros = await servicio.ObtenerAsync();

        Assert.Equal(TimeSpan.FromHours(2), parametros.AntelacionMinima);
        Assert.Equal(TimeSpan.FromDays(30), parametros.AntelacionMaxima);
        Assert.Equal(TimeSpan.FromHours(2), parametros.LimiteCancelacion);
        Assert.Equal(TimeSpan.FromMinutes(15), parametros.VentanaCheckIn);
        Assert.Equal(2, parametros.MaxReservasVigentesPorTelefono);
        Assert.Equal(8, parametros.MaxPersonasPorReserva);
        Assert.Equal(7, parametros.Horarios.Count);
        Assert.All(parametros.Horarios.Values, h =>
        {
            Assert.True(h.Abierto);
            Assert.Equal(new TimeOnly(12, 0), h.HoraApertura);
            Assert.Equal(new TimeOnly(22, 30), h.HoraCierre);
        });
        Assert.Empty(parametros.DiasCerrados);
    }

    [Fact]
    public async Task ObtenerAsync_ConConfiguracionGuardada_LaUsaTalCual()
    {
        var (servicio, repositorio) = Crear();
        repositorio.Guardada = new ConfiguracionGuardada(
            ConfiguracionDePrueba(),
            [new HorarioOperacion
            {
                DiaSemana = DayOfWeek.Monday, Abierto = false,
                HoraApertura = new TimeOnly(9, 0), HoraCierre = new TimeOnly(17, 0)
            }],
            [new DiaCerrado { Fecha = new DateOnly(2026, 12, 25), Motivo = "Navidad" }]);

        var parametros = await servicio.ObtenerAsync();

        Assert.Equal(TimeSpan.FromMinutes(60), parametros.AntelacionMinima);
        Assert.Equal(TimeSpan.FromDays(90), parametros.AntelacionMaxima);
        Assert.Equal(TimeSpan.FromMinutes(30), parametros.LimiteCancelacion);
        Assert.Equal(TimeSpan.FromMinutes(45), parametros.VentanaCheckIn);
        Assert.Equal(5, parametros.MaxReservasVigentesPorTelefono);
        Assert.Equal(20, parametros.MaxPersonasPorReserva);
        Assert.False(parametros.Horarios[DayOfWeek.Monday].Abierto);
        Assert.Equal(new TimeOnly(9, 0), parametros.Horarios[DayOfWeek.Monday].HoraApertura);
        Assert.Contains(new DateOnly(2026, 12, 25), parametros.DiasCerrados);
    }

    [Fact]
    public async Task ObtenerAsync_ConHorariosIncompletos_CompletaLosDiasQueFaltanConElDefecto()
    {
        var (servicio, repositorio) = Crear();
        repositorio.Guardada = new ConfiguracionGuardada(
            ConfiguracionDePrueba(),
            [new HorarioOperacion
            {
                DiaSemana = DayOfWeek.Sunday, Abierto = false,
                HoraApertura = new TimeOnly(10, 0), HoraCierre = new TimeOnly(14, 0)
            }],
            []);

        var parametros = await servicio.ObtenerAsync();

        Assert.Equal(7, parametros.Horarios.Count);
        Assert.False(parametros.Horarios[DayOfWeek.Sunday].Abierto);
        Assert.True(parametros.Horarios[DayOfWeek.Tuesday].Abierto);
        Assert.Equal(new TimeOnly(22, 30), parametros.Horarios[DayOfWeek.Tuesday].HoraCierre);
    }

    [Fact]
    public async Task ObtenerAsync_ConHorariosGuardadosPeroSinFilaDeConfiguracion_UsaLosHorariosYLosDefaultsDeLasPoliticas()
    {
        var (servicio, repositorio) = Crear();
        repositorio.Guardada = new ConfiguracionGuardada(
            null,
            [new HorarioOperacion
            {
                DiaSemana = DayOfWeek.Friday, Abierto = true,
                HoraApertura = new TimeOnly(11, 0), HoraCierre = new TimeOnly(23, 0)
            }],
            []);

        var parametros = await servicio.ObtenerAsync();

        Assert.Equal(new TimeOnly(11, 0), parametros.Horarios[DayOfWeek.Friday].HoraApertura);
        Assert.Equal(2, parametros.MaxReservasVigentesPorTelefono);
    }

    [Fact]
    public async Task ObtenerAsync_DosLlamadas_LeeUnaSolaVezYDevuelveLaMismaInstantanea()
    {
        var (servicio, repositorio) = Crear();

        var primera = await servicio.ObtenerAsync();
        var segunda = await servicio.ObtenerAsync();

        Assert.Equal(1, repositorio.Lecturas);
        Assert.Same(primera, segunda);
    }

    [Fact]
    public async Task Invalidar_ObligaALeerDeNuevoYSeVeElCambio()
    {
        var (servicio, repositorio) = Crear();
        var antes = await servicio.ObtenerAsync();
        repositorio.Guardada = new ConfiguracionGuardada(ConfiguracionDePrueba(), [], []);

        var sinInvalidar = await servicio.ObtenerAsync();
        servicio.Invalidar();
        var despues = await servicio.ObtenerAsync();

        Assert.Equal(2, antes.MaxReservasVigentesPorTelefono);
        Assert.Equal(2, sinInvalidar.MaxReservasVigentesPorTelefono); // sigue en caché
        Assert.Equal(5, despues.MaxReservasVigentesPorTelefono);
        Assert.Equal(2, repositorio.Lecturas);
    }

    [Fact]
    public void EstaAbiertoYHorarioDelDia_RespetanElDiaDeLaSemanaYLasFechasCerradas()
    {
        var parametros = ParametrosReservaService.PorDefecto() with
        {
            Horarios = ParametrosReservaService.PorDefecto().Horarios
                .ToDictionary(par => par.Key, par => par.Key == DayOfWeek.Monday ? par.Value with { Abierto = false } : par.Value),
            DiasCerrados = new HashSet<DateOnly> { new(2026, 12, 25) } // viernes
        };

        Assert.False(parametros.EstaAbierto(new DateOnly(2026, 12, 21))); // lunes cerrado
        Assert.Null(parametros.HorarioDelDia(new DateOnly(2026, 12, 21)));
        Assert.False(parametros.EstaAbierto(new DateOnly(2026, 12, 25))); // fecha cerrada
        Assert.True(parametros.EstaAbierto(new DateOnly(2026, 12, 24))); // jueves normal
        Assert.Equal(new HorarioDia(true, new TimeOnly(12, 0), new TimeOnly(22, 30)), parametros.HorarioDelDia(new DateOnly(2026, 12, 24)));
    }

    [Fact]
    public void EstaAbierto_ConUnDiaSinHorario_LoConsideraCerrado()
    {
        var parametros = ParametrosReservaService.PorDefecto() with
        {
            Horarios = new Dictionary<DayOfWeek, HorarioDia>()
        };

        Assert.False(parametros.EstaAbierto(new DateOnly(2026, 12, 24)));
    }
}
