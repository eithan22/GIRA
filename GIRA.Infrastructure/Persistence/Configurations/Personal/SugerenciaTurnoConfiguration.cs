using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="SugerenciaTurno"/>: tabla en el esquema <c>personal</c>, estado como texto
/// y búsqueda por estado y fecha.
/// </summary>
public class SugerenciaTurnoConfiguration : IEntityTypeConfiguration<SugerenciaTurno>
{
    /// <summary>Nombre del índice por estado y fecha, usado al vencer sugerencias pendientes.</summary>
    public const string IndiceEstadoFecha = "IX_SugerenciaTurno_Estado_Fecha";

    /// <summary>Configura la tabla, el estado y el índice.</summary>
    public void Configure(EntityTypeBuilder<SugerenciaTurno> builder)
    {
        builder.ToTable("SugerenciaTurno", "personal");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(s => new { s.Estado, s.Fecha })
            .HasDatabaseName(IndiceEstadoFecha);

        builder.HasIndex(s => s.EventoId)
            .HasDatabaseName("IX_SugerenciaTurno_EventoId");
    }
}
