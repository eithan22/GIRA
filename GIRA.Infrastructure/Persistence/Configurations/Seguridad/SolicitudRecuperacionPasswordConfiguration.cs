using GIRA.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración Fluent API de <see cref="SolicitudRecuperacionPassword"/>.
/// </summary>
public class SolicitudRecuperacionPasswordConfiguration : IEntityTypeConfiguration<SolicitudRecuperacionPassword>
{
    /// <summary>Configura la tabla, la clave y el índice único por usuario.</summary>
    public void Configure(EntityTypeBuilder<SolicitudRecuperacionPassword> builder)
    {
        builder.ToTable("SolicitudRecuperacionPassword", "seguridad");

        builder.HasKey(s => s.Id);

        // Una sola solicitud pendiente por usuario: el índice único lo garantiza en la base
        // aunque dos peticiones simultáneas pasen la comprobación previa del servicio.
        builder.HasIndex(s => s.UsuarioId)
            .IsUnique();
    }
}
