using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="UnidadMedida"/>.</summary>
public class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    /// <summary>Índice único (no eliminadas) del nombre.</summary>
    public const string IndiceNombre = "IX_UnidadMedida_Nombre";

    /// <summary>Índice único (no eliminadas) de la abreviatura.</summary>
    public const string IndiceAbreviatura = "IX_UnidadMedida_Abreviatura";

    /// <summary>Configura la tabla y los índices únicos.</summary>
    public void Configure(EntityTypeBuilder<UnidadMedida> builder)
    {
        builder.ToTable("UnidadMedida", "inventario");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Abreviatura).HasMaxLength(10).IsRequired();
        builder.HasIndex(u => u.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);
        builder.HasIndex(u => u.Abreviatura).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceAbreviatura);
    }
}
