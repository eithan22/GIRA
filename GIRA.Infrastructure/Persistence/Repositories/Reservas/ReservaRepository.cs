using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IReservaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class ReservaRepository : IReservaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public ReservaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        return await _contexto.Reservas
            .Where(r => r.Fecha == fecha
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken)
    {
        return _contexto.Reservas
            .Join(_contexto.Clientes, r => r.ClienteId, c => c.Id, (r, c) => new { r, c })
            .Where(x => x.c.Telefono == telefono
                && (x.r.Estado == EstadoReserva.Confirmada || x.r.Estado == EstadoReserva.EnCurso))
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken)
    {
        return _contexto.Reservas
            .Join(_contexto.Clientes, r => r.ClienteId, c => c.Id, (r, c) => new { r, c })
            .Where(x => x.r.Codigo == codigo && x.c.Telefono == telefono)
            .Select(x => x.r)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReservaListadoDto>> ListarAsync(
        DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = ConsultaCompleta();

        if (fecha is not null)
        {
            consulta = consulta.Where(x => x.Reserva.Fecha == fecha);
        }

        if (estado is not null)
        {
            consulta = consulta.Where(x => x.Reserva.Estado == estado);
        }

        if (franjaId is not null)
        {
            consulta = consulta.Where(x => x.Reserva.FranjaHorariaId == franjaId);
        }

        if (!string.IsNullOrEmpty(telefono))
        {
            consulta = consulta.Where(x => x.Cliente.Telefono == telefono);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderByDescending(x => x.Reserva.Fecha)
            .ThenBy(x => x.Franja.HoraInicio)
            .ThenBy(x => x.Mesa.Numero)
            .ThenBy(x => x.Reserva.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = filas.Select(x => new ReservaListadoDto(
            x.Reserva.Id, x.Reserva.Codigo, x.Reserva.Estado.ToString(), x.Reserva.Fecha,
            x.Franja.Id, x.Franja.Nombre, x.Franja.HoraInicio, x.Franja.HoraFin,
            x.Mesa.Numero, x.Zona.Nombre, x.Reserva.Personas, x.Reserva.PersonasReales,
            x.Cliente.Nombre, x.Cliente.Telefono, x.Reserva.Origen.ToString())).ToList();

        return new PagedResult<ReservaListadoDto>(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _contexto.Reservas.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken)
    {
        var x = await ConsultaCompleta().FirstOrDefaultAsync(f => f.Reserva.Id == id, cancellationToken);
        if (x is null)
        {
            return null;
        }

        return new ReservaDetalleDto(
            x.Reserva.Id, x.Reserva.Codigo, x.Reserva.Estado.ToString(), x.Reserva.Fecha,
            x.Franja.Id, x.Franja.Nombre, x.Franja.HoraInicio, x.Franja.HoraFin,
            x.Mesa.Id, x.Mesa.Numero, x.Mesa.Capacidad, x.Zona.Nombre,
            x.Reserva.Personas, x.Reserva.PersonasReales, x.Reserva.FechaCheckIn,
            x.Cliente.Id, x.Cliente.Nombre, x.Cliente.Telefono, x.Reserva.Origen.ToString(),
            x.Reserva.MotivoCancelacion, x.Reserva.CreatedAt);
    }

    // Reserva solo guarda claves (sin navegaciones): se une explícitamente con cliente, mesa, zona y franja.
    private IQueryable<FilaAgenda> ConsultaCompleta() =>
        from r in _contexto.Reservas
        join c in _contexto.Clientes on r.ClienteId equals c.Id
        join m in _contexto.Mesas on r.MesaId equals m.Id
        join z in _contexto.Zonas on m.ZonaId equals z.Id
        join f in _contexto.FranjasHorarias on r.FranjaHorariaId equals f.Id
        select new FilaAgenda { Reserva = r, Cliente = c, Mesa = m, Zona = z, Franja = f };

    // Clase con inicializador (no record con constructor): EF solo traduce la proyección de entidades así.
    private sealed class FilaAgenda
    {
        public Reserva Reserva { get; init; } = null!;
        public Cliente Cliente { get; init; } = null!;
        public Mesa Mesa { get; init; } = null!;
        public Zona Zona { get; init; } = null!;
        public FranjaHoraria Franja { get; init; } = null!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(
        EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken)
    {
        var filas = await (
                from r in _contexto.Reservas
                where r.Estado == estado && r.Fecha <= fechaMaxima
                join f in _contexto.FranjasHorarias on r.FranjaHorariaId equals f.Id
                orderby r.Fecha, f.HoraInicio, r.Id
                select new FilaFranja { Reserva = r, HoraInicio = f.HoraInicio, HoraFin = f.HoraFin })
            .Take(limite)
            .ToListAsync(cancellationToken);

        return filas.Select(x => new ReservaConFranja(x.Reserva, x.HoraInicio, x.HoraFin)).ToList();
    }

    /// <inheritdoc />
    public void Descartar(Reserva reserva)
    {
        _contexto.Entry(reserva).State = EntityState.Detached;
    }

    // Clase con inicializador (no record con constructor): EF solo traduce la proyección de entidades así.
    private sealed class FilaFranja
    {
        public Reserva Reserva { get; init; } = null!;
        public TimeOnly HoraInicio { get; init; }
        public TimeOnly HoraFin { get; init; }
    }

    /// <inheritdoc />
    public Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken)
    {
        return _contexto.Reservas.AnyAsync(
            r => r.MesaId == mesaId && r.Fecha == fecha && r.FranjaHorariaId == franjaId
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        return _contexto.Reservas.AnyAsync(r => r.Codigo == codigo, cancellationToken);
    }

    /// <inheritdoc />
    public void Agregar(Reserva reserva)
    {
        _contexto.Reservas.Add(reserva);
    }

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ReservaConflictoConcurrenciaException(
                "La reserva cambió mientras se procesaba la cancelación; vuelve a consultarla.", ex);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, "IX_Reserva_MesaId_Fecha_FranjaHorariaId_Vigente"))
        {
            throw new ReservaDuplicadaException(
                "Esa mesa ya fue tomada, intenta otra franja.", ex);
        }
    }
}
