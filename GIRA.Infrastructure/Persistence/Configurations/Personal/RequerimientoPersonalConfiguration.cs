using GIRA.Domain.Entities.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="RequerimientoPersonal"/>: tabla en el esquema <c>personal</c>,
/// un solo requerimiento por cargo entre registros vigentes y relación con Cargo.
/// </summary>
public class RequerimientoPersonalConfiguration : IEntityTypeConfiguration<RequerimientoPersonal>
{
    /// <summary>Nombre del índice único por cargo.</summary>
    public const string IndiceCargo = "IX_RequerimientoPersonal_CargoId";

    /// <summary>Configura la tabla, el índice único por cargo y la relación.</summary>
    public void Configure(EntityTypeBuilder<RequerimientoPersonal> builder)
    {
        builder.ToTable("RequerimientoPersonal", "personal");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.CargoId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName(IndiceCargo);

        builder.HasOne<Cargo>()
            .WithMany()
            .HasForeignKey(r => r.CargoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
