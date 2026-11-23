using GIRA.Domain.Entities.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Personal;

/// <summary>
/// Configuración Fluent API de <see cref="EventoPrediccionDescartado"/>: tabla en el esquema <c>personal</c>,
/// motivo acotado a 250 caracteres e índice por identificador de evento.
/// </summary>
public class EventoPrediccionDescartadoConfiguration : IEntityTypeConfiguration<EventoPrediccionDescartado>
{
    /// <summary>Configura la tabla, el motivo y el índice.</summary>
    public void Configure(EntityTypeBuilder<EventoPrediccionDescartado> builder)
    {
        builder.ToTable("EventoPrediccionDescartado", "personal");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Motivo)
            .HasMaxLength(250)
            .IsRequired();

        builder.HasIndex(e => e.EventoId)
            .HasDatabaseName("IX_EventoPrediccionDescartado_EventoId");
    }
}
