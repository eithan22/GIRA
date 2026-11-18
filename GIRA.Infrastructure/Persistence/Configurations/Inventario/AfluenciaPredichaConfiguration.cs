using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="AfluenciaPredicha"/>: una fila por fecha.</summary>
public class AfluenciaPredichaConfiguration : IEntityTypeConfiguration<AfluenciaPredicha>
{
    /// <summary>Índice único (no eliminadas) de la fecha.</summary>
    public const string IndiceFecha = "IX_AfluenciaPredicha_Fecha";

    /// <summary>Configura la tabla y el índice único.</summary>
    public void Configure(EntityTypeBuilder<AfluenciaPredicha> builder)
    {
        builder.ToTable("AfluenciaPredicha", "inventario", t =>
            t.HasCheckConstraint("CK_AfluenciaPredicha_Comensales", "[Comensales] >= 0"));
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Fecha).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceFecha);
    }
}
