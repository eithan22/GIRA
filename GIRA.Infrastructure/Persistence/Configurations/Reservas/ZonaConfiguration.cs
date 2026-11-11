using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Zona"/>.
/// </summary>
public class ZonaConfiguration : IEntityTypeConfiguration<Zona>
{
    /// <summary>Nombre del índice único (filtrado por no eliminadas) sobre el nombre de la zona.</summary>
    public const string IndiceNombre = "IX_Zona_Nombre";

    /// <summary>Configura la tabla y el índice único del nombre.</summary>
    public void Configure(EntityTypeBuilder<Zona> builder)
    {
        builder.ToTable("Zona", "reservas");

        builder.HasKey(z => z.Id);

        builder.Property(z => z.Nombre)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(z => z.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);
        // Activa: GI-163 la desactiva; si la zona está inactiva, ninguna de sus mesas se
        // ofrece en disponibilidad aunque cada mesa esté activa (RN-RES-21).
    }
}
