using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Mesa"/>.
/// </summary>
public class MesaConfiguration : IEntityTypeConfiguration<Mesa>
{
    /// <summary>Nombre del índice único (filtrado por no eliminadas) sobre el número de mesa, global en el restaurante.</summary>
    public const string IndiceNumero = "IX_Mesa_Numero";

    /// <summary>Configura la tabla, el índice único del número y la relación con la zona.</summary>
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("Mesa", "reservas");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.Numero).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNumero);
        // Activa: GI-163 la desactiva; GI-157 excluye mesas inactivas de la disponibilidad.

        builder.HasOne<Zona>()
            .WithMany()
            .HasForeignKey(m => m.ZonaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
