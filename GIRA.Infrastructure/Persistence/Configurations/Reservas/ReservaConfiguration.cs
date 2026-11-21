using GIRA.Domain.Entities.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Reservas;

/// <summary>
/// Configuración Fluent API de <see cref="Reserva"/>.
/// </summary>
public class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    /// <summary>
    /// Configura la tabla, el código único, las relaciones con Cliente/Mesa/FranjaHoraria
    /// y el índice único filtrado que impide doble reserva.
    /// </summary>
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("Reserva", "reservas");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Codigo)
            .HasMaxLength(8)
            .IsFixedLength()
            .IsRequired();

        builder.Property(r => r.MotivoCancelacion)
            .HasMaxLength(500);

        // Se guardan al hacer check-in (HacerCheckIn); las usa el cierre automático (GI-224)
        // para publicar VisitaCompletadaEvent sin intervención manual.
        builder.Property(r => r.PersonasReales);
        builder.Property(r => r.FechaCheckIn);

        builder.HasIndex(r => r.Codigo)
            .IsUnique();

        // "Vigente" significa Estado Confirmada o EnCurso: la reserva todavía ocupa la mesa.
        // El índice es único SOLO entre reservas vigentes en la misma mesa, fecha y franja,
        // así una reserva Cancelada, NoAsistio o Completada no bloquea esa mesa para una
        // reserva nueva (se liberó el cupo).
        // SQL Server no permite columnas calculadas en el predicado de un índice filtrado
        // (se intentó con una columna EsVigente y falló); por eso el filtro va directo sobre
        // la columna real Estado, con los valores de EstadoReserva (0 = Confirmada, 1 = EnCurso).
        builder.HasIndex(r => new { r.MesaId, r.Fecha, r.FranjaHorariaId })
            .IsUnique()
            .HasFilter("[Estado] IN (0, 1)")
            .HasDatabaseName("IX_Reserva_MesaId_Fecha_FranjaHorariaId_Vigente");

        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Mesa>()
            .WithMany()
            .HasForeignKey(r => r.MesaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FranjaHoraria>()
            .WithMany()
            .HasForeignKey(r => r.FranjaHorariaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
