using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Mesa"/>.
/// </summary>
public class MesaConfiguration : IEntityTypeConfiguration<Mesa>
{
    /// <summary>Configura la tabla y la relación con su zona.</summary>
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("Mesa", "reservas");

        builder.HasKey(m => m.Id);

        builder.HasOne<Zona>()
            .WithMany()
            .HasForeignKey(m => m.ZonaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
