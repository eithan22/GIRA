using GIRA.Domain.Entities.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="Disponibilidad"/>: tabla en el esquema <c>personal</c>, relación
/// con el empleado y búsqueda por empleado y día. El solapamiento lo revisa el servicio.
/// </summary>
public class DisponibilidadConfiguration : IEntityTypeConfiguration<Disponibilidad>
{
    /// <summary>Nombre del índice por empleado y día, usado para búsquedas de bloques del día.</summary>
    public const string IndiceEmpleadoDia = "IX_Disponibilidad_EmpleadoId_DiaSemana";

    /// <summary>Configura la tabla, la relación con Empleado y el índice de búsqueda.</summary>
    public void Configure(EntityTypeBuilder<Disponibilidad> builder)
    {
        builder.ToTable("Disponibilidad", "personal");

        builder.HasKey(d => d.Id);

        builder.HasIndex(d => new { d.EmpleadoId, d.DiaSemana })
            .HasDatabaseName(IndiceEmpleadoDia);

        builder.HasOne<Empleado>()
            .WithMany()
            .HasForeignKey(d => d.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
