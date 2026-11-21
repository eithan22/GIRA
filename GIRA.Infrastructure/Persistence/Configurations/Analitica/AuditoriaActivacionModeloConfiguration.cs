using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class AuditoriaActivacionModeloConfiguration : IEntityTypeConfiguration<AuditoriaActivacionModelo>
{
    public void Configure(EntityTypeBuilder<AuditoriaActivacionModelo> builder)
    {
        builder.ToTable("AuditoriaActivacionModelo", "analitica");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Motivo).HasMaxLength(500).IsRequired();
        builder.HasIndex(a => a.ActivadoEn);
        builder.HasOne<ModeloAfluencia>().WithMany().HasForeignKey(a => a.ModeloAnteriorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ModeloAfluencia>().WithMany().HasForeignKey(a => a.ModeloActivadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
