using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class GeneracionPrediccionConfiguration : IEntityTypeConfiguration<GeneracionPrediccion>
{
    public void Configure(EntityTypeBuilder<GeneracionPrediccion> builder)
    {
        builder.ToTable("GeneracionPrediccion", "analitica",
            tabla => tabla.HasCheckConstraint("CK_GeneracionPrediccion_Cantidad", "[CantidadPredicciones] >= 0"));
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => g.FechaProceso).IsUnique();
    }
}
