using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>
/// Implementación de <see cref="IOcupacionRepository"/> contra <see cref="GiraDbContext"/>. Solo lectura:
/// todas las consultas son <c>AsNoTracking</c> y proyectan directo a los registros de lectura, sin cargar entidades
/// ni el teléfono del cliente. El filtro global de borrado lógico sigue aplicando; las zonas y mesas inactivas sí se traen.
/// </summary>
public class OcupacionRepository : IOcupacionRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public OcupacionRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public Task<FranjaOcupacionLectura?> ObtenerFranjaAsync(Guid franjaId, CancellationToken cancellationToken)
    {
        return _contexto.FranjasHorarias
            .AsNoTracking()
            .Where(f => f.Id == franjaId)
            .Select(f => new FranjaOcupacionLectura(f.Id, f.Nombre, f.HoraInicio, f.HoraFin))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ZonaOcupacionLectura>> ListarZonasAsync(CancellationToken cancellationToken)
    {
        return await _contexto.Zonas
            .AsNoTracking()
            .Select(z => new ZonaOcupacionLectura(z.Id, z.Nombre, z.Activa))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MesaOcupacionLectura>> ListarMesasAsync(CancellationToken cancellationToken)
    {
        return await _contexto.Mesas
            .AsNoTracking()
            .Select(m => new MesaOcupacionLectura(m.Id, m.ZonaId, m.Numero, m.Capacidad, m.Activa))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReservaVigenteLectura>> ListarReservasVigentesAsync(
        DateOnly fecha, Guid franjaId, CancellationToken cancellationToken)
    {
        return await _contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Fecha == fecha && r.FranjaHorariaId == franjaId
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso))
            .Join(_contexto.Clientes.AsNoTracking(), r => r.ClienteId, c => c.Id, (r, c) => new { r, c })
            .Select(x => new ReservaVigenteLectura(x.r.MesaId, x.r.Id, x.r.Estado, x.c.Nombre, x.r.Personas, x.r.PersonasReales))
            .ToListAsync(cancellationToken);
    }
}
