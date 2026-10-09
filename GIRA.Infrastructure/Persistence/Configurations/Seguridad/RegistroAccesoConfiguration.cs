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

        builder.HasIndex(r => new { r.UsuarioId, r.Fecha });
    }
}
