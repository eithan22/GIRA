using GIRA.Domain.Entities.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Analitica;

/// <summary>Mapeo de los resultados de predicción de afluencia.</summary>
public sealed class PrediccionResultadoConfiguration : IEntityTypeConfiguration<PrediccionResultado>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PrediccionResultado> builder)
    {
        builder.ToTable("PrediccionResultado", "analitica");
        builder.HasKey(prediccion => prediccion.Id);
        builder.Property(prediccion => prediccion.Fecha).HasColumnType("date");
        builder.Property(prediccion => prediccion.HoraInicio).HasColumnType("time(0)");
        builder.Property(prediccion => prediccion.AfluenciaEstimada).IsRequired();
        builder.Property(prediccion => prediccion.FechaGeneracion).HasColumnType("datetime2");
        builder.Property(prediccion => prediccion.Modelo).HasMaxLength(40).IsRequired();
        builder.Property(prediccion => prediccion.RowVersion).IsRowVersion();
        builder.HasIndex(prediccion => new { prediccion.Fecha, prediccion.HoraInicio }).IsUnique();
    }
}
