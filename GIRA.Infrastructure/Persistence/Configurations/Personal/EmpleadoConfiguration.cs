using GIRA.Domain.Entities.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="Empleado"/>: tabla en el esquema <c>personal</c>, cédula única
/// entre empleados vigentes y relación obligatoria con su cargo.
/// </summary>
public class EmpleadoConfiguration : IEntityTypeConfiguration<Empleado>
{
    /// <summary>Nombre del índice único de cédula; lo usa el repositorio para reconocer el choque.</summary>
    public const string IndiceCedula = "IX_Empleado_Cedula";

    /// <summary>Configura la tabla, las longitudes, el índice único y la relación con Cargo.</summary>
    public void Configure(EntityTypeBuilder<Empleado> builder)
    {
        builder.ToTable("Empleado", "personal");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Cedula).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Telefono).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(256);

        builder.HasIndex(e => e.Cedula)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName(IndiceCedula);

        builder.HasOne<Cargo>()
            .WithMany()
            .HasForeignKey(e => e.CargoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
