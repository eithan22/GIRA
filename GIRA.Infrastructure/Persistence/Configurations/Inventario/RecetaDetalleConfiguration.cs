using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="RecetaDetalle"/>: un insumo por línea de receta.</summary>
public class RecetaDetalleConfiguration : IEntityTypeConfiguration<RecetaDetalle>
{
    /// <summary>Índice único (no eliminadas): un insumo aparece una sola vez en cada receta.</summary>
    public const string IndiceRecetaInsumo = "IX_RecetaDetalle_RecetaId_InsumoId";

    /// <summary>Configura la tabla, la cantidad, el índice único y la relación con el insumo.</summary>
    public void Configure(EntityTypeBuilder<RecetaDetalle> builder)
    {
        builder.ToTable("RecetaDetalle", "inventario");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Cantidad).HasPrecision(18, 3);
        builder.HasIndex(d => new { d.RecetaId, d.InsumoId }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceRecetaInsumo);

        builder.HasOne(d => d.Insumo).WithMany().HasForeignKey(d => d.InsumoId).OnDelete(DeleteBehavior.Restrict);
    }
}
