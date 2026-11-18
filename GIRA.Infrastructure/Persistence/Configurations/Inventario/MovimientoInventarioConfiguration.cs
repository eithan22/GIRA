using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="MovimientoInventario"/> (el kardex).</summary>
public class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    /// <summary>Índice único de visita e insumo: una visita descuenta cada insumo una sola vez.</summary>
    public const string IndiceVisitaInsumo = "IX_MovimientoInventario_VisitaId_InsumoId";

    /// <summary>Configura la tabla, las cantidades y los índices del kardex y de las salidas por visita.</summary>
    public void Configure(EntityTypeBuilder<MovimientoInventario> builder)
    {
        builder.HasIndex(m => new { m.VisitaId, m.InsumoId }).IsUnique()
            .HasFilter("[VisitaId] IS NOT NULL AND [IsDeleted] = 0").HasDatabaseName(IndiceVisitaInsumo);
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
