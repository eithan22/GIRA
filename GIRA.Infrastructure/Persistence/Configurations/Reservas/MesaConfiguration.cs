using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Mesa"/>.
/// </summary>
public class MesaConfiguration : IEntityTypeConfiguration<Mesa>
{
    public const string IndiceZonaNumero = "IX_Mesa_ZonaId_Numero";
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("Mesa", "reservas");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.ZonaId, m.Numero }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceZonaNumero);
        // Activa: GI-163 la desactiva; GI-157 excluye mesas inactivas de la disponibilidad.

        builder.HasOne<Zona>()
            .WithMany()
            .HasForeignKey(m => m.ZonaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
