using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class ModeloAfluenciaConfiguration : IEntityTypeConfiguration<ModeloAfluencia>
{
    public void Configure(EntityTypeBuilder<ModeloAfluencia> builder)
    {
        builder.ToTable("ModeloAfluencia", "analitica", table =>
        {
            table.HasCheckConstraint("CK_ModeloAfluencia_Metricas", "[Mae] >= 0 AND [Rmse] >= 0");
            table.HasCheckConstraint("CK_ModeloAfluencia_Fechas",
                "[EntrenamientoDesde] <= [EntrenamientoHasta] AND [ValidacionDesde] <= [ValidacionHasta] AND [EntrenamientoHasta] < [ValidacionDesde]");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Version).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ArchivoModelo).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(m => m.Mae).HasPrecision(18, 4);
        builder.Property(m => m.Rmse).HasPrecision(18, 4);
        builder.HasOne<FranjaHoraria>().WithMany().HasForeignKey(m => m.FranjaHorariaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EntrenamientoModelo>().WithMany().HasForeignKey(m => m.EntrenamientoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FranjaHorariaId, m.Version }).IsUnique();
        builder.HasIndex(m => m.FranjaHorariaId).IsUnique().HasFilter("[Activo] = 1");
    }
}
