using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="Insumo"/>.</summary>
public class InsumoConfiguration : IEntityTypeConfiguration<Insumo>
{
    /// <summary>Índice único (no eliminados) del nombre.</summary>
    public const string IndiceNombre = "IX_Insumo_Nombre";

    /// <summary>Configura la tabla, las cantidades, el índice único del nombre y las relaciones con los catálogos.</summary>
    public void Configure(EntityTypeBuilder<Insumo> builder)
    {
        builder.ToTable("Insumo", "inventario");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CantidadStock).HasPrecision(18, 3);
        builder.Property(i => i.PuntoReorden).HasPrecision(18, 3);
        builder.Property(i => i.CostoUnitario).HasPrecision(18, 2);
        builder.HasIndex(i => i.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);

        builder.HasOne(i => i.CategoriaInsumo).WithMany().HasForeignKey(i => i.CategoriaInsumoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.UnidadMedida).WithMany().HasForeignKey(i => i.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Proveedor).WithMany().HasForeignKey(i => i.ProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}
