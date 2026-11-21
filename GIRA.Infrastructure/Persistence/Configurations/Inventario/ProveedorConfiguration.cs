using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="Proveedor"/>.</summary>
public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    /// <summary>Índice único (no eliminados) del nombre.</summary>
    public const string IndiceNombre = "IX_Proveedor_Nombre";

    /// <summary>Configura la tabla y el índice único del nombre.</summary>
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("Proveedor", "inventario");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Telefono).HasMaxLength(10).IsRequired();
        builder.HasIndex(p => p.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);
    }
}
