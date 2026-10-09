using GIRA.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración Fluent API de <see cref="RefreshToken"/>.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <summary>Configura la tabla, la clave y el índice único del hash del token.</summary>
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken", "seguridad");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();
    }
}
