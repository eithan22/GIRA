using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Zona"/>.
/// </summary>
public class ZonaConfiguration : IEntityTypeConfiguration<Zona>
{
    /// <summary>Configura la tabla.</summary>
    public void Configure(EntityTypeBuilder<Zona> builder)
    {
        builder.ToTable("Zona", "reservas");

        builder.HasKey(z => z.Id);

        builder.Property(z => z.Nombre)
            .HasMaxLength(100)
            .IsRequired();
    }
}
