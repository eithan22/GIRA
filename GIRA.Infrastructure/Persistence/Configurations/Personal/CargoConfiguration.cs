using GIRA.Domain.Entities.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="Cargo"/>: tabla en el esquema <c>personal</c>,
/// longitudes y nombre único.
/// </summary>
public class CargoConfiguration : IEntityTypeConfiguration<Cargo>
{
    /// <summary>Nombre del índice único de nombre; lo usa el repositorio para reconocer el choque.</summary>
    public const string IndiceNombre = "IX_Cargo_Nombre";

    /// <summary>Configura la tabla, las longitudes y el índice único por nombre (solo entre cargos vigentes).</summary>
    public void Configure(EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("Cargo", "personal");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Descripcion)
            .HasMaxLength(250);

        builder.HasIndex(c => c.Nombre)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName(IndiceNombre);
    }
}
