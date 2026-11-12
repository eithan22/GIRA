using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Repository.Reservas;

public interface IReservasFuturasRepository
{
    Task<int> ContarPorMesaAsync(Guid mesaId, DateOnly desde, CancellationToken cancellationToken);
    Task<int> MaximoPersonasPorMesaAsync(Guid mesaId, DateOnly desde, CancellationToken cancellationToken);
    Task<int> ContarPorZonaAsync(Guid zonaId, DateOnly desde, CancellationToken cancellationToken);
    Task<int> ContarPorFranjaAsync(Guid franjaId, DateOnly desde, CancellationToken cancellationToken);
}