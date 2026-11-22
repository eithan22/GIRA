using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="LoteInsumo"/> (entradas de perecederos con su vencimiento).</summary>
public class LoteInsumoConfiguration : IEntityTypeConfiguration<LoteInsumo>
{
    /// <summary>Configura la tabla, la cantidad y el índice por vencimiento.</summary>
    public void Configure(EntityTypeBuilder<LoteInsumo> builder)
    {
        builder.ToTable("LoteInsumo", "inventario");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Cantidad).HasPrecision(18, 3);
        builder.Property(l => l.CostoUnitario).HasPrecision(18, 4);
        builder.HasIndex(l => new { l.InsumoId, l.FechaVencimiento });

        builder.HasOne<Insumo>().WithMany().HasForeignKey(l => l.InsumoId).OnDelete(DeleteBehavior.Restrict);
    }
}
