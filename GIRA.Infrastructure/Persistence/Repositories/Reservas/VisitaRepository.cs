using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IVisitaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class VisitaRepository : IVisitaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public VisitaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public void Agregar(VisitaSinReserva visita)
    {
        _contexto.VisitasSinReserva.Add(visita);
    }

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        return _contexto.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VisitasDelDiaLectura> ListarPorFechaAsync(DateOnly fecha, int limite, CancellationToken cancellationToken)
    {
        // Los totales salen de la tabla de visitas sola (no del join): cuentan todo el día aunque la lista se recorte.
        var delDia = _contexto.VisitasSinReserva.Where(v => v.Fecha == fecha);
        var total = await delDia.CountAsync(cancellationToken);
        var personas = total == 0 ? 0 : await delDia.SumAsync(v => v.PersonasReales, cancellationToken);

        // Left join con franja y mesa: si alguna ya no existe (borrado lógico) la visita sigue en la lista.
        var filas = await (
                from v in delDia
                join f in _contexto.FranjasHorarias on v.FranjaHorariaId equals f.Id into franjas
                from f in franjas.DefaultIfEmpty()
                join m in _contexto.Mesas on v.MesaId equals m.Id into mesas
                from m in mesas.DefaultIfEmpty()
                orderby v.CreatedAt descending, v.Id descending
                select new FilaVisita
                {
                    Id = v.Id,
                    FranjaNombre = f == null ? null : f.Nombre,
                    MesaNumero = m == null ? (int?)null : m.Numero,
                    Personas = v.PersonasReales,
                    CreadaEnUtc = v.CreatedAt
                })
            .Take(limite)
            .ToListAsync(cancellationToken);

        return new VisitasDelDiaLectura(
            total,
            personas,
            filas.Select(x => new VisitaListadoLectura(x.Id, x.FranjaNombre, x.MesaNumero, x.Personas, x.CreadaEnUtc)).ToList());
    }

    // Clase con inicializador (no record con constructor): EF solo traduce así la proyección con left join.
    private sealed class FilaVisita
    {
        public Guid Id { get; init; }
        public string? FranjaNombre { get; init; }
        public int? MesaNumero { get; init; }
        public int Personas { get; init; }
        public DateTime CreadaEnUtc { get; init; }
    }
}
