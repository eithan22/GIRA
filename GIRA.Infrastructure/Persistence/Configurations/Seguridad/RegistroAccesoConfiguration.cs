using GIRA.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración Fluent API de <see cref="RegistroAcceso"/>.
/// </summary>
public class RegistroAccesoConfiguration : IEntityTypeConfiguration<RegistroAcceso>
{
    /// <summary>Configura la tabla, la clave y el índice por usuario y fecha.</summary>
    public void Configure(EntityTypeBuilder<RegistroAcceso> builder)
    {
        builder.ToTable("RegistroAcceso", "seguridad");

        builder.HasKey(r => r.Id);

        // Se escribe en cada intento de login, incluso de usuarios no autenticados;
        // sin límite de longitud es un vector para llenar la tabla de auditoría a propósito.
        builder.Property(r => r.CorreoIntentado)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.Ip)
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(r => r.Motivo)
            .HasMaxLength(200);

        builder.HasIndex(r => new { r.UsuarioId, r.Fecha });
    }
}
