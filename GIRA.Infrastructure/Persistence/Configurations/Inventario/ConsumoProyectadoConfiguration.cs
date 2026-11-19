using GIRA.Domain.Entities.Inventario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Inventario;

/// <summary>Configuración Fluent API de <see cref="ConsumoProyectado"/>: una fila por fecha e insumo.</summary>
public class ConsumoProyectadoConfiguration : IEntityTypeConfiguration<ConsumoProyectado>
{
    /// <summary>Índice único (no eliminadas) de fecha e insumo.</summary>
    public const string IndiceFechaInsumo = "IX_ConsumoProyectado_Fecha_InsumoId";

    /// <summary>Configura la tabla, la cantidad y el índice único.</summary>
    public void Configure(EntityTypeBuilder<ConsumoProyectado> builder)
    {
        builder.ToTable("ConsumoProyectado", "inventario");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CantidadEsperada).HasPrecision(18, 3);
        builder.HasIndex(c => new { c.Fecha, c.InsumoId }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceFechaInsumo);

        builder.HasOne<Insumo>().WithMany().HasForeignKey(c => c.InsumoId).OnDelete(DeleteBehavior.Restrict);
    }
}
