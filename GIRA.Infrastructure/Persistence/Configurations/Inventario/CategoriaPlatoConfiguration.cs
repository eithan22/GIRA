using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="CategoriaPlato"/>.</summary>
public class CategoriaPlatoConfiguration : IEntityTypeConfiguration<CategoriaPlato>
{
    /// <summary>Índice único (no eliminadas) del nombre.</summary>
    public const string IndiceNombre = "IX_CategoriaPlato_Nombre";

    /// <summary>Configura la tabla y el índice único del nombre.</summary>
    public void Configure(EntityTypeBuilder<CategoriaPlato> builder)
    {
        builder.ToTable("CategoriaPlato", "inventario");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);
    }
}
