using System.Collections.Concurrent;
using GIRA.Application.Events.Reservas;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GIRA.Tests.Reservas;

/// <summary>Registro compartido de los eventos de visita que reciben los handlers de prueba.</summary>
public sealed class RegistroEventos
{
    /// <summary>Eventos recibidos, en orden.</summary>
    public ConcurrentQueue<VisitaCompletadaEvent> Visitas { get; } = new();

    /// <summary>Cancelaciones recibidas, en orden.</summary>
    public ConcurrentQueue<ReservaCanceladaEvent> Cancelaciones { get; } = new();

    /// <summary>Modificaciones recibidas, en orden.</summary>
    public ConcurrentQueue<ReservaModificadaEvent> Modificaciones { get; } = new();
}

/// <summary>Handler de prueba que anota cada <see cref="VisitaCompletadaEvent"/> recibido.</summary>
public sealed class HandlerRegistrador : INotificationHandler<VisitaCompletadaEvent>
{
    private readonly RegistroEventos _registro;

    /// <summary>Crea el handler con el registro compartido.</summary>
    public HandlerRegistrador(RegistroEventos registro) => _registro = registro;

    /// <inheritdoc />
    public Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken)
    {
        _registro.Visitas.Enqueue(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Handler de prueba que siempre falla, para comprobar que no revierte ni rompe la operación.</summary>
public sealed class HandlerQueFalla : INotificationHandler<VisitaCompletadaEvent>
{
    /// <inheritdoc />
    public Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Fallo simulado de un handler.");
}

/// <summary>Handler de prueba que anota cada <see cref="ReservaCanceladaEvent"/> recibido.</summary>
public sealed class HandlerCancelacionRegistrador : INotificationHandler<ReservaCanceladaEvent>
{
    private readonly RegistroEventos _registro;

    /// <summary>Crea el handler con el registro compartido.</summary>
    public HandlerCancelacionRegistrador(RegistroEventos registro) => _registro = registro;

    /// <inheritdoc />
    public Task Handle(ReservaCanceladaEvent notification, CancellationToken cancellationToken)
    {
        _registro.Cancelaciones.Enqueue(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Handler de prueba para la cancelación que siempre falla.</summary>
public sealed class HandlerCancelacionQueFalla : INotificationHandler<ReservaCanceladaEvent>
{
    /// <inheritdoc />
    public Task Handle(ReservaCanceladaEvent notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Fallo simulado de un handler.");
}

/// <summary>Handler de prueba que anota cada <see cref="ReservaModificadaEvent"/> recibido.</summary>
public sealed class HandlerModificacionRegistrador : INotificationHandler<ReservaModificadaEvent>
{
    private readonly RegistroEventos _registro;

    /// <summary>Crea el handler con el registro compartido.</summary>
    public HandlerModificacionRegistrador(RegistroEventos registro) => _registro = registro;

    /// <inheritdoc />
    public Task Handle(ReservaModificadaEvent notification, CancellationToken cancellationToken)
    {
        _registro.Modificaciones.Enqueue(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Handler de prueba para la modificación que siempre falla.</summary>
public sealed class HandlerModificacionQueFalla : INotificationHandler<ReservaModificadaEvent>
{
    /// <inheritdoc />
    public Task Handle(ReservaModificadaEvent notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Fallo simulado de un handler.");
}

/// <summary>Publicador que siempre lanza, como un handler que falla.</summary>
class PublisherQueFalla : IPublisher
{
    public int Intentos { get; private set; }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Intentos++;
        throw new InvalidOperationException("Fallo simulado de un handler.");
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        Intentos++;
        throw new InvalidOperationException("Fallo simulado de un handler.");
    }
}

/// <summary>Publicador que ejecuta una comprobación en el momento de publicar (para verificar el orden).</summary>
class PublisherQueVerifica : IPublisher
{
    private readonly Func<Task> _alPublicar;
    public List<object> Publicados { get; } = [];

    public PublisherQueVerifica(Func<Task> alPublicar) => _alPublicar = alPublicar;

    public async Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        await _alPublicar();
        Publicados.Add(notification);
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        await _alPublicar();
        Publicados.Add(notification!);
    }
}

/// <summary>Logger que guarda lo que se registra, para comprobar nivel y contenido.</summary>
sealed class LoggerCapturador<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensaje, Exception? Excepcion)> Entradas { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entradas.Add((logLevel, formatter(state, exception), exception));
}
