using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="Turno"/>: tabla en el esquema <c>personal</c>, estado como texto,
/// relaciones con Empleado y Cargo, y búsqueda por empleado y fecha. El solapamiento lo revisa el servicio.
/// </summary>
public class TurnoConfiguration : IEntityTypeConfiguration<Turno>
{
    /// <summary>Nombre del índice por empleado y fecha, usado para revisar solapamientos.</summary>
    public const string IndiceEmpleadoFecha = "IX_Turno_EmpleadoId_Fecha";

    /// <summary>Configura la tabla, el estado, las notas y las relaciones.</summary>
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("Turno", "personal");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Notas).HasMaxLength(250);

        builder.HasIndex(t => new { t.EmpleadoId, t.Fecha })
            .HasDatabaseName(IndiceEmpleadoFecha);

        builder.HasOne<Empleado>()
            .WithMany()
            .HasForeignKey(t => t.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cargo>()
            .WithMany()
            .HasForeignKey(t => t.CargoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
