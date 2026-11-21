using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Consumo proyectado por fecha e insumo.</summary>
public class ConsumoProyectadoRepository : IConsumoProyectadoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public ConsumoProyectadoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsumoProyectado>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        await _contexto.ConsumosProyectados
            .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Agregar(ConsumoProyectado consumo) => _contexto.ConsumosProyectados.Add(consumo);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AfluenciaPredicha>> ListarPrediccionesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        await _contexto.AfluenciasPredichas
            .Where(a => a.Fecha >= desde && a.Fecha <= hasta)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void AgregarPrediccion(AfluenciaPredicha prediccion) => _contexto.AfluenciasPredichas.Add(prediccion);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);
}
