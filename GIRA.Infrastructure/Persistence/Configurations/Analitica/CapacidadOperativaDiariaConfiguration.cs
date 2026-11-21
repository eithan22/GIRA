using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class CapacidadOperativaDiariaConfiguration : IEntityTypeConfiguration<CapacidadOperativaDiaria>
{
    public void Configure(EntityTypeBuilder<CapacidadOperativaDiaria> builder)
    {
        builder.ToTable("CapacidadOperativaDiaria", "analitica", tabla =>
            tabla.HasCheckConstraint("CK_CapacidadOperativaDiaria_Capacidad", "[CapacidadTotal] >= 0"));
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Fecha).IsUnique();
    }
}
