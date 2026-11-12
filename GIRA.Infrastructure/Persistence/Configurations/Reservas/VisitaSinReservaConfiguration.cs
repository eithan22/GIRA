using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="VisitaSinReserva"/>.
/// </summary>
public class VisitaSinReservaConfiguration : IEntityTypeConfiguration<VisitaSinReserva>
{
    /// <summary>Configura la tabla y las relaciones con Mesa (opcional) y FranjaHoraria.</summary>
    public void Configure(EntityTypeBuilder<VisitaSinReserva> builder)
    {
        builder.ToTable("VisitaSinReserva", "reservas");

        builder.HasKey(v => v.Id);

        builder.HasOne<Mesa>()
            .WithMany()
            .HasForeignKey(v => v.MesaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FranjaHoraria>()
            .WithMany()
            .HasForeignKey(v => v.FranjaHorariaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
