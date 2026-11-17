using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="FranjaHoraria"/>. Las franjas por defecto ya no se siembran aquí
/// (sin <c>HasData</c>): las inserta <c>FranjasPorDefectoSeeder</c> y la migración <c>Reservas_FranjasDeDosHoras</c>,
/// solo si no existen, para no pisar lo que el Gerente edite.
/// </summary>
public class FranjaHorariaConfiguration : IEntityTypeConfiguration<FranjaHoraria>
{
    /// <summary>Nombre del índice único (filtrado por no eliminadas) sobre el nombre de la franja.</summary>
    public const string IndiceNombre = "IX_FranjaHoraria_Nombre";

    /// <summary>Configura la tabla y el índice único del nombre.</summary>
    public void Configure(EntityTypeBuilder<FranjaHoraria> builder)
    {
        builder.ToTable("FranjaHoraria", "reservas");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nombre)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(f => f.Nombre).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName(IndiceNombre);
        // Activa: GI-163 la desactiva; GI-157 excluye franjas inactivas de la disponibilidad.
    }
}
