using GIRA.Domain.Entities.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Configuracion;

/// <summary>Configuración Fluent API de <see cref="Notificacion"/>.</summary>
public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    /// <summary>Configura la tabla y el índice de la bandeja (usuario, leída, fecha).</summary>
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("Notificacion", "configuracion");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Destinatario).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Tipo).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Mensaje).HasMaxLength(500).IsRequired();
        builder.HasIndex(n => new { n.UsuarioId, n.Leida, n.CreatedAt });
    }
}
