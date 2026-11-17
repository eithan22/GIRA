using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;


public class ReservasFuturasRepository : IReservasFuturasRepository
{
    private readonly GiraDbContext _contexto;

    public ReservasFuturasRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<int> ContarPorMesaAsync(Guid mesaId, DateOnly desde, CancellationToken cancellationToken)
    {
        return Vigentes(desde).CountAsync(r => r.MesaId == mesaId, cancellationToken);
    }

    public async Task<int> MaximoPersonasPorMesaAsync(Guid mesaId, DateOnly desde, CancellationToken cancellationToken)
    {
        var maximo = await Vigentes(desde)
            .Where(r => r.MesaId == mesaId)
            .MaxAsync(r => (int?)r.Personas, cancellationToken);

        return maximo ?? 0;
    }

    public Task<int> ContarPorZonaAsync(Guid zonaId, DateOnly desde, CancellationToken cancellationToken)
    {
        return Vigentes(desde)
            .Where(r => _contexto.Mesas.Any(m => m.Id == r.MesaId && m.ZonaId == zonaId))
            .CountAsync(cancellationToken);
    }

    public Task<int> ContarPorFranjaAsync(Guid franjaId, DateOnly desde, CancellationToken cancellationToken)
    {
        return Vigentes(desde).CountAsync(r => r.FranjaHorariaId == franjaId, cancellationToken);
    }

    public async Task<int> ContarPorDiaSemanaAsync(DayOfWeek dia, DateOnly desde, CancellationToken cancellationToken)
    {
        // Se traen solo las fechas (de reservas vigentes futuras) y se cuentan en memoria: no depende de cómo
        // SQL Server traduzca el día de la semana de un DateOnly.
        var fechas = await Vigentes(desde).Select(r => r.Fecha).ToListAsync(cancellationToken);
        return fechas.Count(fecha => fecha.DayOfWeek == dia);
    }

    // "Vigente" = Confirmada o EnCurso, igual que el índice único de doble reserva.
    private IQueryable<Reserva> Vigentes(DateOnly desde)
    {
        return _contexto.Reservas.Where(r => r.Fecha >= desde
            && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso));
    }
}