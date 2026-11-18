using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>Configuración Fluent API de <see cref="Ausencia"/>: tabla en el esquema <c>personal</c>, enums como texto y relación con Empleado.</summary>
public class AusenciaConfiguration : IEntityTypeConfiguration<Ausencia>
{
    /// <summary>Configura la tabla, los enums, el motivo y la relación con el empleado.</summary>
    public void Configure(EntityTypeBuilder<Ausencia> builder)
    {
        builder.ToTable("Ausencia", "personal");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Motivo).HasMaxLength(250);

        builder.HasIndex(a => a.EmpleadoId);

        builder.HasOne<Empleado>()
            .WithMany()
            .HasForeignKey(a => a.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
