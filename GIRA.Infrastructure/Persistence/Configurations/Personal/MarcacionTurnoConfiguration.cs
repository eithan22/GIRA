using GIRA.Domain.Entities.Personal;
using GIRA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

public sealed class MarcacionTurnoConfiguration : IEntityTypeConfiguration<MarcacionTurno>
{
    public void Configure(EntityTypeBuilder<MarcacionTurno> builder)
    {
        builder.ToTable("MarcacionTurno", "personal", tabla =>
        {
            tabla.HasCheckConstraint("CK_MarcacionTurno_Pausa", "[MinutosPausa] >= 0");
            tabla.HasCheckConstraint("CK_MarcacionTurno_Rango",
                "[FinEn] IS NULL OR [FinEn] > [InicioEn]");
        });
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.UsuarioId).IsUnique().HasFilter("[FinEn] IS NULL");
        builder.Property(m => m.InicioEn).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
