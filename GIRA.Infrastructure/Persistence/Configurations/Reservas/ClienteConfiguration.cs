using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Cliente"/>.
/// </summary>
public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    /// <summary>Configura la tabla y el índice único del teléfono.</summary>
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Cliente", "reservas");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Telefono)
            .HasMaxLength(10)
            .IsFixedLength()
            .IsRequired();

        builder.HasIndex(c => c.Telefono)
            .IsUnique();
    }
}
