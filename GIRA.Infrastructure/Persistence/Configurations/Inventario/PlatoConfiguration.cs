using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="Plato"/>.</summary>
public class PlatoConfiguration : IEntityTypeConfiguration<Plato>
{
    /// <summary>Índice único (no eliminados) del nombre.</summary>
    public const string IndiceNombre = "IX_Plato_Nombre";

    /// <summary>Configura la tabla, el índice único del nombre y las relaciones con su categoría y su receta.</summary>
    public void Configure(EntityTypeBuilder<Plato> builder)
    {
        builder.ToTable("Plato", "inventario");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Precio).HasPrecision(18, 2);
        builder.Property(p => p.PorcentajeDemanda).HasPrecision(5, 2);
        builder.HasIndex(p => p.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);

        builder.HasOne(p => p.CategoriaPlato).WithMany().HasForeignKey(p => p.CategoriaPlatoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Receta).WithOne().HasForeignKey<Receta>(r => r.PlatoId).OnDelete(DeleteBehavior.Restrict);
    }
}
