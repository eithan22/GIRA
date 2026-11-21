using GIRA.Domain.Entities.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Configuracion;

/// <summary>Configuración Fluent API de <see cref="HorarioOperacion"/>.</summary>
public class HorarioOperacionConfiguration : IEntityTypeConfiguration<HorarioOperacion>
{
    /// <summary>Nombre del índice único (filtrado por no eliminadas) sobre el día de la semana.</summary>
    public const string IndiceDia = "IX_HorarioOperacion_DiaSemana";

    /// <summary>Configura la tabla y el índice único por día.</summary>
    public void Configure(EntityTypeBuilder<HorarioOperacion> builder)
    {
        builder.ToTable("HorarioOperacion", "configuracion", tabla =>
            tabla.HasCheckConstraint("CK_HorarioOperacion_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.DiaSemana).HasConversion<int>();
        builder.HasIndex(h => h.DiaSemana).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceDia);
    }
}
