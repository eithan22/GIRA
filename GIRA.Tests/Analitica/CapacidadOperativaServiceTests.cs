using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Tests.Common;

namespace GIRA.Tests.Analitica;

public sealed class CapacidadOperativaServiceTests
{
    private static readonly DateOnly Fecha = new(2026, 11, 18);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CapturaLaCapacidadAntesDeAbrir_YNoDuplicaElSnapshotDelDia()
    {
        var repositorio = new CapacidadRepositoryFalso { CapacidadActual = 24 };
        var servicio = new CapacidadOperativaService(
            repositorio,
            new ParametrosReservaFalso(Parametros(abierto: true)),
            new TimeProviderFalso(Ahora));

        await servicio.CapturarAntesDeAperturaAsync(Fecha, new TimeOnly(9, 0), default);
        await servicio.CapturarAntesDeAperturaAsync(Fecha, new TimeOnly(9, 1), default);

        var snapshot = Assert.Single(repositorio.Guardados);
        Assert.Equal(24, snapshot.CapacidadTotal);
        Assert.Equal(Ahora, snapshot.CapturadaEn);
        Assert.Equal(1, repositorio.Calculos);
    }

    [Fact]
    public async Task NoCreaUnSnapshotTardio_QueImplicariaUsarLaConfiguracionActualEnElPasado()
    {
        var repositorio = new CapacidadRepositoryFalso();
        var servicio = new CapacidadOperativaService(
            repositorio,
            new ParametrosReservaFalso(Parametros(abierto: true)),
            new TimeProviderFalso(Ahora));

        await servicio.CapturarAntesDeAperturaAsync(Fecha, new TimeOnly(10, 0), default);

        Assert.Empty(repositorio.Guardados);
        Assert.Equal(0, repositorio.Calculos);
    }

    [Fact]
    public async Task RegistraCapacidadCeroEnDiaCerrado()
    {
        var repositorio = new CapacidadRepositoryFalso();
        var servicio = new CapacidadOperativaService(
            repositorio,
            new ParametrosReservaFalso(Parametros(abierto: false)),
            new TimeProviderFalso(Ahora));

        await servicio.CapturarAntesDeAperturaAsync(Fecha, new TimeOnly(9, 0), default);

        Assert.Equal(0, Assert.Single(repositorio.Guardados).CapacidadTotal);
        Assert.Equal(0, repositorio.Calculos);
    }

    private static ParametrosReserva Parametros(bool abierto)
    {
        var horarios = new Dictionary<DayOfWeek, HorarioDia>
        {
            [Fecha.DayOfWeek] = new(abierto, new TimeOnly(10, 0), new TimeOnly(22, 0))
        };
        return new ParametrosReserva(
            TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            1, 1, null, horarios, new HashSet<DateOnly>());
    }

    private sealed class ParametrosReservaFalso(ParametrosReserva valor) : IParametrosReservaService
    {
        public Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(valor);

        public void Invalidar() { }
    }

    private sealed class CapacidadRepositoryFalso : ICapacidadOperativaRepository
    {
        public int CapacidadActual { get; set; }
        public int Calculos { get; private set; }
        public List<CapacidadOperativaDiaria> Guardados { get; } = [];

        public Task<bool> ExisteAsync(DateOnly fecha, CancellationToken cancellationToken) =>
            Task.FromResult(Guardados.Any(c => c.Fecha == fecha));

        public Task<int> CalcularCapacidadActualAsync(CancellationToken cancellationToken)
        {
            Calculos++;
            return Task.FromResult(CapacidadActual);
        }

        public Task GuardarAsync(CapacidadOperativaDiaria capacidad, CancellationToken cancellationToken)
        {
            Guardados.Add(capacidad);
            return Task.CompletedTask;
        }
    }
}
