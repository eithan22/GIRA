using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

public sealed class RegistroAfluenciaConfiguration : IEntityTypeConfiguration<RegistroAfluencia>
{
    public const string IndiceOrigenReferencia = "IX_RegistroAfluencia_Origen_ReferenciaId";

    public void Configure(EntityTypeBuilder<RegistroAfluencia> builder)
    {
        builder.ToTable("RegistroAfluencia", "analitica", tabla =>
        {
            tabla.HasCheckConstraint("CK_RegistroAfluencia_Personas", "[Personas] > 0");
            tabla.HasCheckConstraint("CK_RegistroAfluencia_Referencia", "[ReferenciaId] <> '00000000-0000-0000-0000-000000000000'");
            tabla.HasCheckConstraint("CK_RegistroAfluencia_Fecha", "[Fecha] > '0001-01-01'");
            tabla.HasCheckConstraint("CK_RegistroAfluencia_TipoOrigen",
                "([Tipo] = 1 AND [Origen] IN (1, 2)) OR ([Tipo] = 2 AND [Origen] = 1)");
        });
        builder.HasKey(r => r.Id);
        builder.HasOne<FranjaHoraria>().WithMany().HasForeignKey(r => r.FranjaHorariaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.Origen, r.ReferenciaId })
            .IsUnique().HasDatabaseName(IndiceOrigenReferencia);
        builder.HasIndex(r => new { r.Fecha, r.FranjaHorariaId });
    }
}
