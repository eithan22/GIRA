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
    /// <summary>
    /// Hace obligatorio el nombre completo, crea el índice único filtrado de EmpleadoId
    /// y hace único el índice de correo que Identity crea por defecto.
    /// </summary>
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

        // Identity crea "EmailIndex" sobre NormalizedEmail sin unicidad, aunque las
        // opciones de Identity exijan RequireUniqueEmail = true. Sin esto, la unicidad
        // del correo solo se valida en código (UserManager), no en la base de datos.
        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
