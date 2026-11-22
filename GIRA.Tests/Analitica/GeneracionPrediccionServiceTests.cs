using GIRA.Application.DTOs.Analitica;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Analitica;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Analitica;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Analitica;

public sealed class GeneracionPrediccionServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private readonly Guid _almuerzo = Guid.NewGuid();
    private readonly Guid _cena = Guid.NewGuid();

    [Fact]
    public async Task GeneraYPublicaSoloDiasConTodasLasFranjasYNoRepiteGeneracion()
    {
        var repo = new Repositorio([_almuerzo, _cena]);
        var historial = new Historial([
            Registro(Hoy.AddDays(-6), _almuerzo, 10),
            Registro(Hoy.AddDays(-6), _cena, 5),
            Registro(Hoy.AddDays(-5), _almuerzo, 7)
        ]);
        var publicador = new Publicador(repo);
        var servicio = Crear(repo, historial, publicador);

        await servicio.EjecutarAsync(CancellationToken.None);
        await servicio.EjecutarAsync(CancellationToken.None);

        Assert.Equal(1, repo.VecesGuardado);
        Assert.Equal(6, repo.Predicciones.Count);
        Assert.Equal(2, publicador.Eventos.Count);
        Assert.Equal(new[] { Hoy.AddDays(1), Hoy.AddDays(8) }, publicador.Eventos.Select(e => e.Desde));
        Assert.All(publicador.Eventos, e =>
        {
            Assert.Equal(e.Desde, e.Hasta);
            Assert.Equal(2, e.Predicciones.Count);
        });
        Assert.All(repo.Publicaciones, p => Assert.NotNull(p.PublicadaEn));
    }

    [Fact]
    public async Task FalloAlPublicar_ConservaPendienteYReintentaSinGenerarOtraVez()
    {
        var repo = new Repositorio([_almuerzo]);
        var historial = new Historial([Registro(Hoy.AddDays(-6), _almuerzo, 10)]);
        var publicador = new Publicador(repo) { Fallar = true };
        var servicio = Crear(repo, historial, publicador);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EjecutarAsync(CancellationToken.None));
        Assert.Equal(1, repo.VecesGuardado);
        Assert.All(repo.Publicaciones, p => Assert.Null(p.PublicadaEn));

        publicador.Fallar = false;
        await servicio.EjecutarAsync(CancellationToken.None);
        Assert.Equal(1, repo.VecesGuardado);
        Assert.Equal(2, publicador.Eventos.Count);
        Assert.All(repo.Publicaciones, p => Assert.NotNull(p.PublicadaEn));
    }

    [Fact]
    public async Task AntesDelCierre_NoGeneraPredicciones()
    {
        var repo = new Repositorio([_almuerzo]);
        var servicio = Crear(repo, new Historial([]), new Publicador(repo),
            new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.Zero));

        await servicio.EjecutarAsync(CancellationToken.None);
        Assert.Equal(0, repo.VecesGuardado);
    }

    [Fact]
    public async Task SinObservaciones_NoMarcaLaGeneracionYReintentaCuandoHayDatos()
    {
        var repo = new Repositorio([_almuerzo]);
        var servicio = Crear(repo, new Historial([]), new Publicador(repo));

        await servicio.EjecutarAsync(CancellationToken.None);

        Assert.Equal(0, repo.VecesGuardado);
        Assert.Empty(repo.Predicciones);
        Assert.Empty(repo.Publicaciones);

        var conDatos = Crear(repo, new Historial([Registro(Hoy.AddDays(-6), _almuerzo, 10)]),
            new Publicador(repo));
        await conDatos.EjecutarAsync(CancellationToken.None);

        Assert.Equal(1, repo.VecesGuardado);
        Assert.All(repo.Predicciones, prediccion => Assert.True(prediccion.BajaConfianza));
        Assert.NotEmpty(repo.Predicciones);
    }

    [Fact]
    public async Task UsaModeloSsaVigente_CuandoTieneObservacionesContinuasHastaHoy()
    {
        var franja = _almuerzo;
        var repo = new Repositorio([franja]);
        var historial = new Historial(Enumerable.Range(0, 15)
            .Select(d => Registro(Hoy.AddDays(-14 + d), franja, 10 + d)).ToList());
        var modelo = new ModeloAfluencia
        {
            Id = Guid.NewGuid(),
            FranjaHorariaId = franja,
            EntrenamientoHasta = Hoy.AddDays(-15),
            Version = "ssa-prueba-v1",
            ArchivoModelo = [1],
            Activo = true
        };
        var prediccionSsa = new PronosticoSsa(
            Enumerable.Repeat(30f, ModeloSsaParametros.HorizonteDias).ToList(),
            Enumerable.Repeat(20f, ModeloSsaParametros.HorizonteDias).ToList(),
            Enumerable.Repeat(40f, ModeloSsaParametros.HorizonteDias).ToList());
        var ssa = new Ssa(prediccionSsa);
        var servicio = Crear(repo, historial, new Publicador(repo), modelos: [modelo], ssa: ssa);

        await servicio.EjecutarAsync(CancellationToken.None);

        var primera = repo.Predicciones.Single(p => p.Fecha == Hoy.AddDays(1));
        Assert.Equal("ssa-prueba-v1", primera.VersionModelo);
        Assert.Equal(30, primera.AfluenciaEstimada);
        Assert.Equal(20m, primera.LimiteInferior);
        Assert.Equal(40m, primera.LimiteSuperior);
        Assert.False(primera.BajaConfianza);
        Assert.Equal(15, Assert.Single(ssa.Observaciones).Count);
    }

    private static GeneracionPrediccionService Crear(Repositorio repo, Historial historial, Publicador publicador,
        DateTimeOffset? ahora = null, IReadOnlyList<ModeloAfluencia>? modelos = null, Ssa? ssa = null) =>
        new(repo, historial, new ModelosActivos(modelos ?? []), ssa ?? new Ssa(), new Parametros(), publicador,
            new Reloj(ahora ?? new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero)),
            NullLogger<GeneracionPrediccionService>.Instance);

    private static HistorialAfluenciaDto Registro(DateOnly fecha, Guid franja, int total) =>
        new(fecha, franja, total, 0, total, 1, 0);

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class Parametros : IParametrosReservaService
    {
        public Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ParametrosReservaService.PorDefecto());
        public void Invalidar() { }
    }

    private sealed class Historial(IReadOnlyList<HistorialAfluenciaDto> datos) : IHistorialAfluenciaService
    {
        public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
            DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) => Task.FromResult(datos);
    }

    private sealed class ModelosActivos(IReadOnlyList<ModeloAfluencia> modelos) : IModelosActivosRepository
    {
        public Task<IReadOnlyList<ModeloAfluencia>> ObtenerModelosActivosAsync(CancellationToken cancellationToken) =>
            Task.FromResult(modelos);
    }

    private sealed class Ssa(PronosticoSsa? pronostico = null) : IModeloSsaService
    {
        public List<IReadOnlyList<float>> Observaciones { get; } = [];

        public EntrenamientoSsa Entrenar(IReadOnlyList<float> observaciones, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public PronosticoSsa Predecir(byte[] archivoModelo, IReadOnlyList<float> nuevasObservaciones,
            CancellationToken cancellationToken)
        {
            Observaciones.Add(nuevasObservaciones);
            return pronostico ?? throw new NotSupportedException();
        }
    }

    private sealed class Repositorio(IReadOnlyList<Guid> franjas) : IGeneracionPrediccionRepository
    {
        public int VecesGuardado { get; private set; }
        public IReadOnlyList<PrediccionAfluencia> Predicciones { get; private set; } = [];
        public IReadOnlyList<PublicacionPrediccion> Publicaciones { get; private set; } = [];
        public Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken) =>
            Task.FromResult(franjas);
        public Task<bool> ExisteAsync(DateOnly fechaProceso, CancellationToken cancellationToken) =>
            Task.FromResult(VecesGuardado > 0);
        public Task GuardarAsync(GeneracionPrediccion generacion, IReadOnlyList<PrediccionAfluencia> predicciones,
            IReadOnlyList<PublicacionPrediccion> publicaciones, CancellationToken cancellationToken)
        {
            VecesGuardado++;
            Predicciones = predicciones;
            Publicaciones = publicaciones;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<PublicacionPendiente>> ObtenerPendientesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublicacionPendiente>>(Publicaciones.Where(p => p.PublicadaEn == null)
                .OrderBy(p => p.Fecha).Select(p => new PublicacionPendiente(p.Id,
                    new PrediccionAfluenciaGeneradaEvent(p.Fecha, p.Fecha, Predicciones
                        .Where(x => x.Fecha == p.Fecha)
                        .Select(x => new AfluenciaEstimadaFranja(x.Fecha, x.FranjaHorariaId, x.AfluenciaEstimada))
                        .ToList()))).ToList());
        public Task MarcarPublicadaAsync(Guid id, DateTimeOffset fecha, CancellationToken cancellationToken)
        {
            Publicaciones.Single(p => p.Id == id).PublicadaEn = fecha;
            return Task.CompletedTask;
        }
    }

    private sealed class Publicador(Repositorio repo) : IPublisher
    {
        public bool Fallar { get; set; }
        public List<PrediccionAfluenciaGeneradaEvent> Eventos { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Assert.Equal(1, repo.VecesGuardado);
            if (Fallar) throw new InvalidOperationException("Fallo de prueba");
            Eventos.Add(Assert.IsType<PrediccionAfluenciaGeneradaEvent>(notification));
            return Task.CompletedTask;
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);
    }
}
