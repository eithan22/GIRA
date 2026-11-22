using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class PrediccionAfluenciaConfiguration : IEntityTypeConfiguration<PrediccionAfluencia>
{
    public void Configure(EntityTypeBuilder<PrediccionAfluencia> builder)
    {
        builder.ToTable("PrediccionAfluencia", "analitica", tabla =>
        {
            tabla.HasCheckConstraint("CK_PrediccionAfluencia_Estimada", "[AfluenciaEstimada] >= 0");
            tabla.HasCheckConstraint("CK_PrediccionAfluencia_Limites",
                "([LimiteInferior] IS NULL AND [LimiteSuperior] IS NULL) OR ([LimiteInferior] IS NOT NULL AND [LimiteSuperior] IS NOT NULL AND [LimiteInferior] >= 0 AND [LimiteSuperior] >= [LimiteInferior])");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.VersionModelo).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LimiteInferior).HasPrecision(18, 2);
        builder.Property(p => p.LimiteSuperior).HasPrecision(18, 2);
        builder.HasOne<FranjaHoraria>().WithMany().HasForeignKey(p => p.FranjaHorariaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.Fecha, p.FranjaHorariaId }).IsUnique()
            .HasFilter("[Vigente] = 1");
        builder.HasIndex(p => new { p.GeneracionId, p.Fecha, p.FranjaHorariaId }).IsUnique();
    }
}
