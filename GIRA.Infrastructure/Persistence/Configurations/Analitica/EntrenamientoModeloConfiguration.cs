using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class EntrenamientoModeloConfiguration : IEntityTypeConfiguration<EntrenamientoModelo>,
    IEntityTypeConfiguration<ResultadoEntrenamientoModelo>
{
    public void Configure(EntityTypeBuilder<EntrenamientoModelo> builder)
    {
        builder.ToTable("EntrenamientoModelo", "analitica", table =>
            table.HasCheckConstraint("CK_EntrenamientoModelo_Estado", "[Estado] BETWEEN 1 AND 4"));
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.SolicitadoEn);
        builder.HasIndex(e => e.Estado)
            .IsUnique()
            .HasFilter("[Estado] IN (1, 2)");
        builder.Property(e => e.Error).HasMaxLength(1000);
        builder.HasMany(e => e.Resultados).WithOne()
            .HasForeignKey(r => r.EntrenamientoId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ResultadoEntrenamientoModelo> builder)
    {
        builder.ToTable("ResultadoEntrenamientoModelo", "analitica", table =>
        {
            table.HasCheckConstraint("CK_ResultadoEntrenamientoModelo_Estado", "[Estado] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_ResultadoEntrenamientoModelo_Observaciones",
                "[ObservacionesEntrenamiento] >= 0 AND [ObservacionesValidacion] >= 0");
        });
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.EntrenamientoId, r.FranjaHorariaId }).IsUnique();
        builder.Property(r => r.Mae).HasPrecision(18, 4);
        builder.Property(r => r.Rmse).HasPrecision(18, 4);
        builder.Property(r => r.MaeReferencia).HasPrecision(18, 4);
        builder.Property(r => r.VersionModelo).HasMaxLength(100);
        builder.HasOne<ModeloAfluencia>().WithMany().HasForeignKey(r => r.ModeloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
