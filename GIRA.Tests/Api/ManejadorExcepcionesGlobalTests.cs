using GIRA.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GIRA.Tests.Api;

public class ManejadorExcepcionesGlobalTests
{
    [Fact]
    public async Task ExcepcionGenerica_RegistraErrorYResponde500Generico()
    {
        var logger = new LoggerDePrueba();
        var manejador = new ManejadorExcepcionesGlobal(logger);
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var excepcion = new InvalidOperationException("mensaje-interno-sensible-de-prueba");

        var manejada = await manejador.TryHandleAsync(contexto, excepcion, CancellationToken.None);

        Assert.True(manejada);
        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);
        Assert.Equal("application/problem+json", contexto.Response.ContentType);
        Assert.Single(logger.Registros, r => r.Nivel == LogLevel.Error && r.Excepcion == excepcion);

        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        var cuerpo = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("mensaje-interno-sensible-de-prueba", cuerpo);
    }

    [Fact]
    public async Task OperationCanceledException_NoRegistraErrorNiEscribeRespuesta_DevuelveFalse()
    {
        var logger = new LoggerDePrueba();
        var manejador = new ManejadorExcepcionesGlobal(logger);
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var manejada = await manejador.TryHandleAsync(contexto, new OperationCanceledException(), CancellationToken.None);

        Assert.False(manejada);
        Assert.Empty(logger.Registros);
        Assert.Equal(0, contexto.Response.Body.Length);
    }

    // Reemplazo manual de un mock: Moq está marcado para quitarse en la Fase 2 por no tener uso real.
    private sealed class LoggerDePrueba : ILogger<ManejadorExcepcionesGlobal>
    {
        public List<(LogLevel Nivel, Exception? Excepcion)> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Registros.Add((logLevel, exception));
    }
}
