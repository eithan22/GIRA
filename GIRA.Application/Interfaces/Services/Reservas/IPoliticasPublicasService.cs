using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Políticas de reserva que se muestran al cliente en /reservar (solo lectura).</summary>
public interface IPoliticasPublicasService
{
    /// <summary>Políticas visibles al cliente, calculadas con la configuración vigente y las mesas activas.</summary>
    Task<PoliticasPublicasDto> ObtenerAsync(CancellationToken cancellationToken);
}
