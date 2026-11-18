using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="MovimientoInventario"/> (el kardex).</summary>
public class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    /// <summary>Configura la tabla, las cantidades y el índice para leer el kardex por insumo y fecha.</summary>
    public void Configure(EntityTypeBuilder<MovimientoInventario> builder)
    {
        builder.ToTable("MovimientoInventario", "inventario");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Cantidad).HasPrecision(18, 3);
        builder.Property(m => m.SaldoResultante).HasPrecision(18, 3);
        builder.Property(m => m.CostoUnitario).HasPrecision(18, 2);
        builder.Property(m => m.Motivo).HasMaxLength(300);
        builder.Property(m => m.Referencia).HasMaxLength(200);
        builder.HasIndex(m => new { m.InsumoId, m.Fecha });

        builder.HasOne<Insumo>().WithMany().HasForeignKey(m => m.InsumoId).OnDelete(DeleteBehavior.Restrict);
    }
}
