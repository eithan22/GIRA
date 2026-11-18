using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="AlertaInventario"/>.</summary>
public class AlertaInventarioConfiguration : IEntityTypeConfiguration<AlertaInventario>
{
    /// <summary>Configura la tabla, las cantidades, el índice por estado y la relación con el insumo.</summary>
    public void Configure(EntityTypeBuilder<AlertaInventario> builder)
    {
        builder.ToTable("AlertaInventario", "inventario");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.StockActual).HasPrecision(18, 3);
        builder.Property(a => a.StockProyectado).HasPrecision(18, 3);
        builder.Property(a => a.Detalle).HasMaxLength(300);
        builder.HasIndex(a => new { a.Estado, a.InsumoId });

        builder.HasOne(a => a.Insumo).WithMany().HasForeignKey(a => a.InsumoId).OnDelete(DeleteBehavior.Restrict);
    }
}
