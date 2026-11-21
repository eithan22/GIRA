using GIRA.Application.Common;
using GIRA.Application.DTOs.Analitica;
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

public sealed class EntrenamientoModeloServiceTests
{
    [Fact]
    public async Task Consultar_DevuelveElNombreDeLaFranja()
    {
        var (_, reloj) = CrearReloj();
        var franja = Guid.NewGuid();
        var repositorio = new Modelos(franja);
        repositorio.Trabajo.Resultados.Add(new ResultadoEntrenamientoModelo
        {
            Id = Guid.NewGuid(),
            EntrenamientoId = repositorio.Trabajo.Id,
            FranjaHorariaId = franja,
            Estado = EstadoResultadoEntrenamiento.Activado,
            ObservacionesEntrenamiento = 56,
            ObservacionesValidacion = 14
        });
        var servicio = Crear(repositorio, new Historial([]), new Ssa(), reloj);

        var trabajo = await servicio.ConsultarAsync(repositorio.Trabajo.Id, CancellationToken.None);

        Assert.Equal("Franja de prueba", Assert.Single(trabajo!.Resultados).Franja);
    }

    [Fact]
    public async Task ProcesarSiguiente_ConUnaFechaSinDatos_NoEntrenaNiReemplazaModelos()
    {
        var (hoy, reloj) = CrearReloj();
        var franja = Guid.NewGuid();
        var historial = CrearHistorial(hoy.AddDays(-70), franja).Skip(1).ToList();
        var repositorio = new Modelos(franja);
        var ssa = new Ssa();
        var servicio = Crear(repositorio, new Historial(historial), ssa, reloj);

        await servicio.ProcesarSiguienteAsync(CancellationToken.None);

        var resultado = Assert.Single(repositorio.Resultados!);
        Assert.Equal(EstadoResultadoEntrenamiento.DatosInsuficientes, resultado.Estado);
        Assert.Equal(69, resultado.ObservacionesEntrenamiento + resultado.ObservacionesValidacion);
        Assert.Empty(repositorio.ModelosGuardados!);
        Assert.Equal(0, ssa.VecesEntrenado);
        Assert.Equal(EstadoEntrenamientoModelo.Completado, repositorio.Trabajo.Estado);
    }

    [Fact]
    public async Task ProcesarSiguiente_Con56Mas14Dias_YMaeMejor_ActivaVersionSsa()
    {
        var (hoy, reloj) = CrearReloj();
        var franja = Guid.NewGuid();
        var repositorio = new Modelos(franja);
        var servicio = Crear(repositorio,
            new Historial(CrearHistorial(hoy.AddDays(-70), franja)),
            new Ssa(devolverBasePerfecta: true), reloj);

        await servicio.ProcesarSiguienteAsync(CancellationToken.None);

        var resultado = Assert.Single(repositorio.Resultados!);
        var modelo = Assert.Single(repositorio.ModelosGuardados!);
        Assert.Equal(56, resultado.ObservacionesEntrenamiento);
        Assert.Equal(14, resultado.ObservacionesValidacion);
        Assert.Equal(0m, resultado.Mae);
        Assert.Equal(0m, resultado.MaeReferencia);
        Assert.Equal(EstadoResultadoEntrenamiento.Activado, resultado.Estado);
        Assert.True(modelo.Activo);
        Assert.Equal(EstadoEntrenamientoModelo.Completado, repositorio.Trabajo.Estado);
    }

    [Fact]
    public async Task ProcesarSiguiente_ConMaePeor_ConservaElModeloReferencia()
    {
        var (hoy, reloj) = CrearReloj();
        var franja = Guid.NewGuid();
        var vigente = new ModeloAfluencia
        {
            Id = Guid.NewGuid(), FranjaHorariaId = franja, Version = "ssa-anterior",
            Mae = 0, Activo = true
        };
        var repositorio = new Modelos(franja, [vigente]);
        var servicio = Crear(repositorio,
            new Historial(CrearHistorial(hoy.AddDays(-70), franja)),
            new Ssa(), reloj);

        await servicio.ProcesarSiguienteAsync(CancellationToken.None);

        var resultado = Assert.Single(repositorio.Resultados!);
        var candidato = Assert.Single(repositorio.ModelosGuardados!);
        Assert.Equal(EstadoResultadoEntrenamiento.NoMejoraModeloVigente, resultado.Estado);
        Assert.False(candidato.Activo);
        Assert.Same(vigente, Assert.Single(await repositorio.ObtenerModelosActivosAsync(default)));
    }

    [Fact]
    public async Task ProcesarSiguiente_AnteFallo_PersisteEstadoFallidoYPublicaElResultado()
    {
        var (_, reloj) = CrearReloj();
        var repositorio = new Modelos(Guid.NewGuid());
        var publicador = new Publicador();
        var servicio = Crear(repositorio, new HistorialFallido(), new Ssa(), reloj, publicador);

        await servicio.ProcesarSiguienteAsync(CancellationToken.None);

        Assert.Equal(EstadoEntrenamientoModelo.Fallido, repositorio.Trabajo.Estado);
        Assert.Equal("El entrenamiento falló. Revisa los registros del servidor.", repositorio.Trabajo.Error);
        Assert.NotNull(repositorio.Trabajo.FinalizadoEn);
        Assert.Empty(repositorio.ModelosGuardados ?? []);
        Assert.IsType<EntrenamientoModeloFallidoEvent>(Assert.Single(publicador.Eventos));
    }

    private static EntrenamientoModeloService Crear(
        Modelos repositorio, IHistorialAfluenciaService historial, Ssa ssa, Reloj reloj,
        Publicador? publicador = null) =>
        new(repositorio, historial, ssa, publicador ?? new Publicador(), reloj,
            NullLogger<EntrenamientoModeloService>.Instance);

    private static List<HistorialAfluenciaDto> CrearHistorial(DateOnly inicio, Guid franja) =>
        Enumerable.Range(0, 70)
            .Select(d => new HistorialAfluenciaDto(inicio.AddDays(d), franja,
                10 + d % 7, 0, 10 + d % 7, 1, 0))
            .ToList();

    private static (DateOnly Hoy, Reloj Reloj) CrearReloj()
    {
        var ahora = new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
        var local = TimeZoneInfo.ConvertTime(ahora, HoraLocalGira.ZonaHoraria);
        return (DateOnly.FromDateTime(local.DateTime), new Reloj(ahora));
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class Historial(IReadOnlyList<HistorialAfluenciaDto> datos) : IHistorialAfluenciaService
    {
        public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
            DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) => Task.FromResult(datos);
    }

    private sealed class HistorialFallido : IHistorialAfluenciaService
    {
        public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
            DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<HistorialAfluenciaDto>>(new InvalidOperationException("Fallo de prueba."));
    }

    private sealed class Ssa(bool devolverBasePerfecta = false) : IModeloSsaService
    {
        public int VecesEntrenado { get; private set; }

        public EntrenamientoSsa Entrenar(IReadOnlyList<float> observaciones, CancellationToken cancellationToken)
        {
            VecesEntrenado++;
            var pronostico = devolverBasePerfecta
                ? observaciones.TakeLast(14).ToList()
                : Enumerable.Repeat(100f, 14).ToList();
            return new EntrenamientoSsa([1], pronostico, Enumerable.Repeat(0f, 14).ToList(),
                Enumerable.Repeat(101f, 14).ToList());
        }

        public PronosticoSsa Predecir(byte[] archivoModelo, IReadOnlyList<float> nuevasObservaciones,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Modelos : IModelosAfluenciaRepository
    {
        private readonly Guid _franja;
        private readonly IReadOnlyList<ModeloAfluencia> _activos;
        private bool _procesado;

        public Modelos(Guid franja, IReadOnlyList<ModeloAfluencia>? activos = null)
        {
            _franja = franja;
            _activos = activos ?? [];
            Trabajo = new EntrenamientoModelo
            {
                Id = Guid.NewGuid(), SolicitadoPor = Guid.NewGuid(),
                Estado = EstadoEntrenamientoModelo.EnCola, SolicitadoEn = DateTimeOffset.UtcNow
            };
        }

        public EntrenamientoModelo Trabajo { get; }
        public IReadOnlyList<ResultadoEntrenamientoModelo>? Resultados { get; private set; }
        public IReadOnlyList<ModeloAfluencia>? ModelosGuardados { get; private set; }

        public Task<Guid> SolicitarEntrenamientoAsync(
            Guid usuarioId, DateTimeOffset solicitadoEn, CancellationToken cancellationToken) =>
            Task.FromResult(Trabajo.Id);

        public Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([_franja]);

        public Task<EntrenamientoModelo?> IniciarSiguienteEntrenamientoAsync(
            DateTimeOffset iniciadoEn, CancellationToken cancellationToken)
        {
            if (_procesado) return Task.FromResult<EntrenamientoModelo?>(null);
            _procesado = true;
            Trabajo.Estado = EstadoEntrenamientoModelo.EnCurso;
            Trabajo.IniciadoEn = iniciadoEn;
            return Task.FromResult<EntrenamientoModelo?>(Trabajo);
        }

        public Task ReencolarEntrenamientoAsync(Guid entrenamientoId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<EntrenamientoModelo?> ObtenerEntrenamientoAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<EntrenamientoModelo?>(id == Trabajo.Id ? Trabajo : null);

        public Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresFranjasAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, string> nombres = ids.Contains(_franja)
                ? new Dictionary<Guid, string> { [_franja] = "Franja de prueba" }
                : new Dictionary<Guid, string>();
            return Task.FromResult(nombres);
        }

        public Task<IReadOnlyList<ModeloAfluencia>> ObtenerModelosActivosAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_activos);

        public Task<IReadOnlyList<ModeloAfluenciaDto>> ObtenerModelosAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModeloAfluenciaDto>>([]);

        public Task GuardarResultadosAsync(Guid entrenamientoId, IReadOnlyList<ResultadoEntrenamientoModelo> resultados,
            IReadOnlyList<ModeloAfluencia> modelos, DateTimeOffset finalizadoEn, CancellationToken cancellationToken)
        {
            Resultados = resultados;
            ModelosGuardados = modelos;
            Trabajo.Estado = EstadoEntrenamientoModelo.Completado;
            Trabajo.FinalizadoEn = finalizadoEn;
            return Task.CompletedTask;
        }

        public Task MarcarEntrenamientoFallidoAsync(
            Guid entrenamientoId, string error, DateTimeOffset finalizadoEn, CancellationToken cancellationToken)
        {
            Trabajo.Estado = EstadoEntrenamientoModelo.Fallido;
            Trabajo.Error = error;
            Trabajo.FinalizadoEn = finalizadoEn;
            return Task.CompletedTask;
        }

        public Task<ResultadoActivacionModelo> ActivarModeloAsync(
            Guid modeloId, Guid usuarioId, string motivo, DateTimeOffset activadoEn, bool esRollback,
            CancellationToken cancellationToken) =>
            Task.FromResult(ResultadoActivacionModelo.NoEncontrado);
    }

    private sealed class Publicador : IPublisher
    {
        public List<INotification> Eventos { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Eventos.Add(Assert.IsAssignableFrom<INotification>(notification));
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification!, cancellationToken);
    }
}
