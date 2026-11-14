using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>
/// Lectura de las políticas de reserva y del horario del restaurante. Ningún servicio de Reservas debe
/// escribir estos valores en el código: siempre se leen de aquí. La implementación (en el módulo
/// Configuración) los lee de la base de datos, con una caché breve que se invalida al guardar.
/// </summary>
public interface IParametrosReservaService
{
    /// <summary>
    /// Instantánea vigente de los parámetros. Si todavía no hay configuración guardada, devuelve los valores
    /// por defecto (<c>ParametrosReservaService.PorDefecto</c>).
    /// </summary>
    Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default);

    /// <summary>Descarta la instantánea en caché; lo llaman los servicios que guardan la configuración.</summary>
    void Invalidar();
}
