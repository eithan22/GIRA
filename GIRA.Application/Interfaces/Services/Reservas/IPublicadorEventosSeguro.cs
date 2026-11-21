using MediatR;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>
/// Publica un evento entre módulos DESPUÉS de haber guardado, sin dejar que un handler que
/// falla rompa la operación original (SRS 3.3, regla 3): el error se registra en el log y no
/// se propaga ni se revierte nada.
/// </summary>
public interface IPublicadorEventosSeguro
{
    /// <summary>
    /// Publica <paramref name="evento"/>. Si algún handler lanza una excepción, la registra con
    /// nivel Error (nombre del evento y <paramref name="referencia"/>, sin datos personales) y
    /// continúa sin propagarla.
    /// </summary>
    /// <param name="evento">Evento a publicar; ya debe haberse guardado la operación que lo origina.</param>
    /// <param name="referencia">Identificadores para el log, por ejemplo <c>ReservaId=...</c>. Nunca datos personales.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task PublicarAsync<TEvento>(TEvento evento, string referencia, CancellationToken cancellationToken)
        where TEvento : INotification;
}
