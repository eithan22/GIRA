using GIRA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración Fluent API de <see cref="Usuario"/>, complementaria al mapeo de tabla
/// que ya hace <c>GiraDbContext.OnModelCreating</c> para las entidades de Identity.
/// </summary>
public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    /// <summary>Hace obligatorio el nombre completo y crea el índice único filtrado de EmpleadoId.</summary>
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.Property(u => u.NombreCompleto)
            .HasMaxLength(150)
            .IsRequired();

        // Índice único filtrado: solo exige unicidad cuando EmpleadoId tiene valor,
        // porque no todo usuario corresponde a un empleado.
        builder.HasIndex(u => u.EmpleadoId)
            .IsUnique()
            .HasFilter("[EmpleadoId] IS NOT NULL");
    }
}
