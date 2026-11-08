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
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, "IX_Reserva_MesaId_Fecha_FranjaHorariaId_Vigente"))
        {
            throw new ReservaDuplicadaException(
                "Esa mesa ya fue tomada, intenta otra franja.", ex);
        }
    }
}
