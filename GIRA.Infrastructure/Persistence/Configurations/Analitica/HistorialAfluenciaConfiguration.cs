using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

/// <summary>Mapeo de los registros históricos de afluencia.</summary>
public sealed class HistorialAfluenciaConfiguration : IEntityTypeConfiguration<HistorialAfluencia>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HistorialAfluencia> builder)
    {
        builder.ToTable("HistorialAfluencia", "analitica");
        builder.HasKey(registro => registro.Id);
        builder.Property(registro => registro.Fecha).HasColumnType("date");
        builder.Property(registro => registro.HoraInicio).HasColumnType("time(0)");
        builder.Property(registro => registro.CantidadComensales).IsRequired();
        builder.Property(registro => registro.RowVersion).IsRowVersion();
        builder.HasIndex(registro => new { registro.Fecha, registro.HoraInicio });
        builder.HasIndex(registro => registro.ReservaId)
            .IsUnique()
            .HasFilter("[ReservaId] IS NOT NULL");
    }
}
