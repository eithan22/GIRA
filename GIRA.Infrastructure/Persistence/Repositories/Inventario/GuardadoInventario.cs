using GIRA.Domain.Exceptions.Inventario;
using GIRA.Infrastructure.Persistence.Configurations.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>
/// Guardado común de los repositorios de Inventario: todos comparten el mismo contexto, así que cualquier
/// guardado puede chocar con cualquier índice único del módulo.
/// </summary>
internal static class GuardadoInventario
{
    private static readonly string[] IndicesUnicos =
    [
        UnidadMedidaConfiguration.IndiceNombre,
        UnidadMedidaConfiguration.IndiceAbreviatura,
        CategoriaInsumoConfiguration.IndiceNombre,
        CategoriaPlatoConfiguration.IndiceNombre,
        ProveedorConfiguration.IndiceNombre,
        InsumoConfiguration.IndiceNombre,
        PlatoConfiguration.IndiceNombre,
        RecetaConfiguration.IndicePlato,
        RecetaDetalleConfiguration.IndiceRecetaInsumo,
        ConsumoProyectadoConfiguration.IndiceFechaInsumo
    ];

    /// <summary>Guarda y traduce los choques de concurrencia y de índice único a excepciones del dominio.</summary>
    public static async Task GuardarAsync(GiraDbContext contexto, CancellationToken cancellationToken)
    {
        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InventarioConflictoConcurrenciaException("Un registro de inventario cambió mientras se guardaba.", ex);
        }
        catch (DbUpdateException ex) when (IndicesUnicos.Any(indice => ViolacionesIndiceUnico.Es(ex, indice)))
        {
            throw new InventarioDuplicadoException("Ya existe un registro de inventario con esos datos.", ex);
        }
    }
}
