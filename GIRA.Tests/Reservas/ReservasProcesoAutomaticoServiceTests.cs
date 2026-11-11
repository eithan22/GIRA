using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Api;
using GIRA.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Tests.Reservas;

/// <summary>Registro compartido del proceso falso: cuenta ciclos y scopes, y fija qué devuelve o lanza en cada ciclo.</summary>
sealed class RegistroProceso
{
    private int _ejecuciones;
    private int _instancias;
    private int _liberadas;

    public SemaphoreSlim Senal { get; } = new(0);
    public Func<int, ResultadoProcesoReservas>? Resultado { get; set; }
    public Func<int, Exception?>? Excepcion { get; set; }
    public int Ejecuciones => Volatile.Read(ref _ejecuciones);
    public int Instancias => Volatile.Read(ref _instancias);
    public int Liberadas => Volatile.Read(ref _liberadas);

    public void NuevaInstancia() => Interlocked.Increment(ref _instancias);
    public void InstanciaLiberada() => Interlocked.Increment(ref _liberadas);
    public int NuevaEjecucion() => Interlocked.Increment(ref _ejecuciones);
}

/// <summary>Proceso falso registrado como Scoped: una instancia por scope, para comprobar un scope por ciclo.</summary>
sealed class ProcesoFalso : IProcesoAutomaticoReservasService, IDisposable
{
    private readonly RegistroProceso _registro;

    public ProcesoFalso(RegistroProceso registro)
    {
        _registro = registro;
        registro.NuevaInstancia();
    }

    public Task<ResultadoProcesoReservas> EjecutarAsync(CancellationToken cancellationToken)
    {
        var numero = _registro.NuevaEjecucion();
        try
        {
            var excepcion = _registro.Excepcion?.Invoke(numero);
            if (excepcion is not null)
            {
                throw excepcion;
            }

            return Task.FromResult(_registro.Resultado?.Invoke(numero) ?? ResultadoProcesoReservas.Vacio);
        }
        finally
        {
            _registro.Senal.Release();
        }
    }

    public Task<ResultadoProcesoReservas> MarcarNoShowsAsync(CancellationToken cancellationToken) => EjecutarAsync(cancellationToken);

    public Task<ResultadoProcesoReservas> CompletarVencidasAsync(CancellationToken cancellationToken) => EjecutarAsync(cancellationToken);

    public void Dispose() => _registro.InstanciaLiberada();
}

/// <summary>
/// Pruebas del <see cref="ReservasProcesoAutomaticoService"/> (GI-161, GI-188): un ciclo con scope nuevo,
/// el bucle con un reloj falso que se avanza a mano (sin esperas reales), el retraso del primer ciclo,
/// la validación del intervalo y el registro desactivable.
/// </summary>
public class ReservasProcesoAutomaticoServiceTests
{
    private sealed class Entorno : IDisposable
    {
        private readonly ServiceProvider _proveedor;

        public RegistroProceso Registro { get; } = new();
        public LoggerCapturador<ReservasProcesoAutomaticoService> Logger { get; } = new();
        public TimeProviderAvanzable Reloj { get; } = new(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
        public ReservasProcesoAutomaticoService Servicio { get; }

        public Entorno(ProcesoAutomaticoReservasOptions? opciones = null)
        {
            var servicios = new ServiceCollection();
            servicios.AddSingleton(Registro);
            servicios.AddScoped<IProcesoAutomaticoReservasService, ProcesoFalso>();
            _proveedor = servicios.BuildServiceProvider();

            Servicio = new ReservasProcesoAutomaticoService(
                _proveedor.GetRequiredService<IServiceScopeFactory>(),
                opciones ?? new ProcesoAutomaticoReservasOptions(),
                Reloj,
                Logger);
        }

        // La espera es por una señal que libera el proceso al ejecutarse; el plazo solo evita colgar la prueba si falla.
        public async Task EsperarCicloAsync() =>
            Assert.True(await Registro.Senal.WaitAsync(TimeSpan.FromSeconds(10)), "El ciclo no se ejecutó.");

        public void Dispose()
        {
            Servicio.Dispose();
            _proveedor.Dispose();
        }
    }

    private static IConfiguration Configuracion(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>($"Reservas:ProcesoAutomatico:{v.Clave}", v.Valor)))
            .Build();

    // --- Un ciclo ---

    [Fact]
    public async Task Cada_Ciclo_Usa_Un_Scope_Nuevo_Que_Se_Libera()
    {
        using var e = new Entorno();

        await e.Servicio.EjecutarCicloAsync(default);
        await e.Servicio.EjecutarCicloAsync(default);

        Assert.Equal(2, e.Registro.Ejecuciones);
        Assert.Equal(2, e.Registro.Instancias);
        Assert.Equal(2, e.Registro.Liberadas);
    }

    [Fact]
    public async Task Registra_El_Resultado_Con_Ids_Y_Conteos_Sin_Datos_Personales_Y_Avisa_Del_Tope()
    {
        using var e = new Entorno();
        var fallida = Guid.NewGuid();
        e.Registro.Resultado = _ => new ResultadoProcesoReservas(
            NoAsistidas: 3, Omitidas: 1,
            Fallos: [new FalloProcesoReserva(fallida, new InvalidOperationException("Fallo simulado."))],
            TopeAlcanzado: true, Tope: 200);

        await e.Servicio.EjecutarCicloAsync(default);

        Assert.Contains(e.Logger.Entradas, x => x.Nivel == LogLevel.Information && x.Mensaje.Contains("3 marcadas") && x.Mensaje.Contains("1 omitidas"));
        var aviso = Assert.Single(e.Logger.Entradas, x => x.Nivel == LogLevel.Warning);
        Assert.Contains("200", aviso.Mensaje);
        var error = Assert.Single(e.Logger.Entradas, x => x.Nivel == LogLevel.Error);
        Assert.Contains(fallida.ToString(), error.Mensaje);
        Assert.IsType<InvalidOperationException>(error.Excepcion);
    }

    [Fact]
    public async Task Registra_Tambien_Las_Visitas_Completadas_Al_Terminar_La_Franja()
    {
        using var e = new Entorno();
        e.Registro.Resultado = _ => new ResultadoProcesoReservas(
            NoAsistidas: 0, Omitidas: 0, Fallos: [], TopeAlcanzado: false, Tope: 200, Completadas: 4);

        await e.Servicio.EjecutarCicloAsync(default);

        var info = Assert.Single(e.Logger.Entradas);
        Assert.Equal(LogLevel.Information, info.Nivel);
        Assert.Contains("4 completadas al terminar su franja", info.Mensaje);
    }

    [Fact]
    public async Task Sin_Nada_Que_Procesar_No_Registra_Nada()
    {
        using var e = new Entorno();

        await e.Servicio.EjecutarCicloAsync(default);

        Assert.Empty(e.Logger.Entradas);
    }

    [Fact]
    public async Task Un_Error_Del_Ciclo_Se_Registra_Y_No_Se_Propaga()
    {
        using var e = new Entorno();
        e.Registro.Excepcion = _ => new InvalidOperationException("Base de datos no disponible.");

        await e.Servicio.EjecutarCicloAsync(default);

        var error = Assert.Single(e.Logger.Entradas);
        Assert.Equal(LogLevel.Error, error.Nivel);
        Assert.IsType<InvalidOperationException>(error.Excepcion);
    }

    [Fact]
    public async Task La_Cancelacion_Si_Se_Propaga()
    {
        using var e = new Entorno();
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();
        e.Registro.Excepcion = _ => new OperationCanceledException(cancelacion.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => e.Servicio.EjecutarCicloAsync(cancelacion.Token));
    }

    // --- El bucle, con reloj falso ---

    [Fact]
    public async Task El_Primer_Ciclo_Espera_El_Retraso_Inicial_Y_Luego_Corre_Cada_Intervalo()
    {
        using var e = new Entorno(); // 15 s de retraso inicial, 60 s de intervalo
        await e.Servicio.StartAsync(default);
        Assert.True(await e.Reloj.EsperarTemporizadorAsync()); // el retraso inicial ya está esperando

        e.Reloj.Avanzar(TimeSpan.FromSeconds(14));
        Assert.Equal(0, e.Registro.Ejecuciones); // 14 s: todavía no (nada pudo dispararse)

        e.Reloj.Avanzar(TimeSpan.FromSeconds(1)); // 15 s: arranca el primer ciclo
        await e.EsperarCicloAsync();
        Assert.Equal(1, e.Registro.Ejecuciones);

        e.Reloj.Avanzar(TimeSpan.FromSeconds(59));
        Assert.Equal(1, e.Registro.Ejecuciones); // 59 s después: aún no toca

        e.Reloj.Avanzar(TimeSpan.FromSeconds(1)); // 60 s después: segundo ciclo
        await e.EsperarCicloAsync();
        Assert.Equal(2, e.Registro.Ejecuciones);

        e.Reloj.Avanzar(TimeSpan.FromSeconds(60)); // tercer ciclo
        await e.EsperarCicloAsync();
        Assert.Equal(3, e.Registro.Ejecuciones);

        await e.Servicio.StopAsync(default);
        e.Reloj.Avanzar(TimeSpan.FromMinutes(10));
        Assert.Equal(3, e.Registro.Ejecuciones); // detenido: no corre más
    }

    [Fact]
    public async Task Sin_Retraso_Inicial_El_Primer_Ciclo_Corre_Al_Arrancar()
    {
        using var e = new Entorno(ProcesoAutomaticoReservasOptions.Desde(Configuracion(("RetrasoInicialSegundos", "0"))));

        await e.Servicio.StartAsync(default);
        await e.EsperarCicloAsync();

        Assert.Equal(1, e.Registro.Ejecuciones);
        await e.Servicio.StopAsync(default);
    }

    [Fact]
    public async Task Un_Ciclo_Que_Falla_No_Detiene_El_Bucle()
    {
        using var e = new Entorno();
        e.Registro.Excepcion = n => n == 1 ? new InvalidOperationException("Fallo del primer ciclo.") : null;
        await e.Servicio.StartAsync(default);
        Assert.True(await e.Reloj.EsperarTemporizadorAsync());

        e.Reloj.Avanzar(TimeSpan.FromSeconds(15));
        await e.EsperarCicloAsync(); // ciclo 1 (falla)
        e.Reloj.Avanzar(TimeSpan.FromSeconds(60));
        await e.EsperarCicloAsync(); // ciclo 2 (bien)

        Assert.Equal(2, e.Registro.Ejecuciones);
        Assert.Single(e.Logger.Entradas, x => x.Nivel == LogLevel.Error);
        await e.Servicio.StopAsync(default);
    }

    [Fact]
    public async Task Detenerlo_Antes_Del_Primer_Ciclo_No_Lanza()
    {
        using var e = new Entorno();
        await e.Servicio.StartAsync(default);

        await e.Servicio.StopAsync(default);

        Assert.Equal(0, e.Registro.Ejecuciones);
        Assert.Empty(e.Logger.Entradas);
    }

    // --- Configuración (A) ---

    [Theory]
    [InlineData("5")]
    [InlineData("60")]
    [InlineData("3600")]
    public void Un_Intervalo_Valido_Se_Respeta_Sin_Advertencias(string valor)
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(Configuracion(("IntervaloSegundos", valor)));

        Assert.Equal(int.Parse(valor), opciones.IntervaloSegundos);
        Assert.Empty(opciones.Advertencias);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("3601")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("99999999999999")]
    public void Un_Intervalo_Invalido_Usa_60_Y_Deja_Una_Advertencia_Sin_Lanzar(string valor)
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(Configuracion(("IntervaloSegundos", valor)));

        Assert.Equal(60, opciones.IntervaloSegundos);
        var advertencia = Assert.Single(opciones.Advertencias);
        Assert.Contains("IntervaloSegundos", advertencia);
        Assert.Contains("entre 5 y 3600", advertencia);
    }

    [Fact]
    public void Sin_Configuracion_Usa_Los_Valores_Por_Defecto_Sin_Advertencias()
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(new ConfigurationBuilder().Build());

        Assert.True(opciones.Habilitado);
        Assert.Equal(60, opciones.IntervaloSegundos);
        Assert.Equal(15, opciones.RetrasoInicialSegundos);
        Assert.Empty(opciones.Advertencias);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("601")]
    [InlineData("pronto")]
    public void Un_Retraso_Inicial_Invalido_Usa_El_Valor_Por_Defecto_Con_Advertencia(string valor)
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(Configuracion(("RetrasoInicialSegundos", valor)));

        Assert.Equal(15, opciones.RetrasoInicialSegundos);
        Assert.Single(opciones.Advertencias);
    }

    [Fact]
    public void Habilitado_Que_No_Es_Booleano_Deja_El_Proceso_Habilitado_Con_Advertencia()
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(Configuracion(("Habilitado", "quizá")));

        Assert.True(opciones.Habilitado);
        Assert.Single(opciones.Advertencias);
    }

    [Fact]
    public async Task Un_Intervalo_Invalido_Se_Registra_Como_Warning_Al_Arrancar_Y_El_Bucle_Usa_60_Segundos()
    {
        var opciones = ProcesoAutomaticoReservasOptions.Desde(
            Configuracion(("IntervaloSegundos", "abc"), ("RetrasoInicialSegundos", "0")));
        using var e = new Entorno(opciones);

        await e.Servicio.StartAsync(default);
        await e.EsperarCicloAsync();

        var aviso = Assert.Single(e.Logger.Entradas, x => x.Nivel == LogLevel.Warning);
        Assert.Contains("IntervaloSegundos", aviso.Mensaje);
        Assert.Contains("'abc'", aviso.Mensaje);

        e.Reloj.Avanzar(TimeSpan.FromSeconds(59));
        Assert.Equal(1, e.Registro.Ejecuciones);
        e.Reloj.Avanzar(TimeSpan.FromSeconds(1));
        await e.EsperarCicloAsync();
        Assert.Equal(2, e.Registro.Ejecuciones); // el intervalo efectivo es 60 s, no el valor inválido
        await e.Servicio.StopAsync(default);
    }

    // --- Registro desactivable ---

    private static ServiceProvider Proveedor(IConfiguration configuracion)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddScoped<IProcesoAutomaticoReservasService, ProcesoFalso>();
        servicios.AddSingleton<RegistroProceso>();
        servicios.AddReservasServicios(configuracion);
        return servicios.BuildServiceProvider();
    }

    [Fact]
    public void Si_Esta_Deshabilitado_No_Se_Registra_Ningun_Servicio_En_Segundo_Plano()
    {
        using var proveedor = Proveedor(Configuracion(("Habilitado", "false")));

        Assert.DoesNotContain(proveedor.GetServices<IHostedService>(), s => s is ReservasProcesoAutomaticoService);
    }

    [Fact]
    public void Por_Defecto_Se_Registra_Un_Solo_Servicio_En_Segundo_Plano()
    {
        using var proveedor = Proveedor(new ConfigurationBuilder().Build());

        Assert.Single(proveedor.GetServices<IHostedService>().OfType<ReservasProcesoAutomaticoService>());
    }

    [Fact]
    public void Una_Configuracion_Invalida_Nunca_Lanza_Al_Registrar_Ni_Al_Resolver()
    {
        var configuracion = Configuracion(
            ("Habilitado", "tal vez"), ("IntervaloSegundos", "-1"), ("RetrasoInicialSegundos", "mañana"));

        using var proveedor = Proveedor(configuracion);

        Assert.Single(proveedor.GetServices<IHostedService>().OfType<ReservasProcesoAutomaticoService>());
    }

    // --- Pruebas HTTP: el proceso nunca arranca (E) ---

    [Fact]
    public void Ninguna_Fabrica_De_Pruebas_HTTP_Arranca_El_Proceso_Automatico()
    {
        using var normal = new GiraApiFactory();
        using var conPruebas = new GiraApiFactoryConPruebas();
        using var conReloj = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));

        foreach (var fabrica in new GiraApiFactory[] { normal, conPruebas, conReloj })
        {
            Assert.DoesNotContain(fabrica.Services.GetServices<IHostedService>(), s => s is ReservasProcesoAutomaticoService);
        }
    }
}
