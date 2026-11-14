using GIRA.Domain.Entities.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Configuracion;

/// <summary>Configuración Fluent API de <see cref="DiaCerrado"/>.</summary>
public class DiaCerradoConfiguration : IEntityTypeConfiguration<DiaCerrado>
{
    /// <summary>Nombre del índice único (filtrado por no eliminadas) sobre la fecha.</summary>
    public const string IndiceFecha = "IX_DiaCerrado_Fecha";

    /// <summary>Configura la tabla y el índice único por fecha.</summary>
    public void Configure(EntityTypeBuilder<DiaCerrado> builder)
    {
        builder.ToTable("DiaCerrado", "configuracion");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Motivo).HasMaxLength(200);
        builder.HasIndex(d => d.Fecha).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceFecha);
    }
}
