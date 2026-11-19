using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="Receta"/>: una por plato.</summary>
public class RecetaConfiguration : IEntityTypeConfiguration<Receta>
{
    /// <summary>Índice único (no eliminadas) del plato: un plato tiene una sola receta.</summary>
    public const string IndicePlato = "IX_Receta_PlatoId";

    /// <summary>Configura la tabla, el índice único del plato y sus líneas.</summary>
    public void Configure(EntityTypeBuilder<Receta> builder)
    {
        builder.ToTable("Receta", "inventario");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Instrucciones).HasMaxLength(2000);
        builder.HasIndex(r => r.PlatoId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndicePlato);

        builder.HasMany(r => r.Detalles).WithOne().HasForeignKey(d => d.RecetaId).OnDelete(DeleteBehavior.Cascade);
    }
}
