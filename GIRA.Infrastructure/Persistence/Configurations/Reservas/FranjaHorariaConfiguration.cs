using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="FranjaHoraria"/>.
/// </summary>
public class FranjaHorariaConfiguration : IEntityTypeConfiguration<FranjaHoraria>
{
    /// <summary>Configura la tabla.</summary>
    public void Configure(EntityTypeBuilder<FranjaHoraria> builder)
    {
        builder.ToTable("FranjaHoraria", "reservas");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nombre)
            .HasMaxLength(50)
            .IsRequired();

        var creadaEn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new FranjaHoraria
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Nombre = "Almuerzo",
                HoraInicio = new TimeOnly(12, 0),
                HoraFin = new TimeOnly(15, 0),
                CreatedAt = creadaEn,
                IsDeleted = false
            },
            new FranjaHoraria
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Nombre = "Cena",
                HoraInicio = new TimeOnly(18, 0),
                HoraFin = new TimeOnly(22, 30),
                CreatedAt = creadaEn,
                IsDeleted = false
            });
    }
}
