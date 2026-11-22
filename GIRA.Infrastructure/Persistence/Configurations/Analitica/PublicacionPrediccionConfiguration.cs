using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class PublicacionPrediccionConfiguration : IEntityTypeConfiguration<PublicacionPrediccion>
{
    public void Configure(EntityTypeBuilder<PublicacionPrediccion> builder)
    {
        builder.ToTable("PublicacionPrediccion", "analitica");
        builder.HasKey(p => p.Id);
        builder.HasOne<GeneracionPrediccion>().WithMany().HasForeignKey(p => p.GeneracionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.GeneracionId, p.Fecha }).IsUnique();
        builder.HasIndex(p => p.PublicadaEn);
    }
}
