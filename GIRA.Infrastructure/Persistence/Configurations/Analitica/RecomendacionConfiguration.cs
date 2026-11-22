using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class RecomendacionConfiguration : IEntityTypeConfiguration<Recomendacion>
{
    public void Configure(EntityTypeBuilder<Recomendacion> builder)
    {
        builder.ToTable("Recomendacion", "analitica", tabla =>
            tabla.HasCheckConstraint("CK_Recomendacion_Estado", "[Estado] IN ('Nueva', 'Vista', 'Descartada')"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Clave).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Tipo).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Mensaje).HasMaxLength(500).IsRequired();
        builder.Property(r => r.Prioridad).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Estado).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Origen).HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Clave).IsUnique();
        builder.HasIndex(r => new { r.Vigente, r.Estado, r.Fecha });
    }
}
