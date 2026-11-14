using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Seed;

/// <summary>
/// Siembra la configuración de reservas por defecto: la fila única de políticas y un horario por
/// cada día de la semana. Solo inserta lo que falta: no modifica nada que ya exista, ni siquiera
/// lo borrado lógicamente (se consulta con <c>IgnoreQueryFilters</c>). Es idempotente y la pueden
/// invocar el arranque en Development y las fixtures de pruebas.
/// </summary>
public static class ConfiguracionReservasSeeder
{
    /// <summary>Inserta la fila de configuración y los horarios de los días que no tengan registro.</summary>
    public static async Task SembrarAsync(GiraDbContext contexto, CancellationToken cancellationToken = default)
    {
        if (!await contexto.ConfiguracionesRestaurante.IgnoreQueryFilters()
                .AnyAsync(c => c.Id == ConfiguracionRestaurante.IdUnico, cancellationToken))
        {
            contexto.ConfiguracionesRestaurante.Add(new ConfiguracionRestaurante
            {
                Id = ConfiguracionRestaurante.IdUnico,
                AntelacionMinimaMinutos = ParametrosReservaService.ValoresPorDefecto.AntelacionMinimaMinutos,
                AntelacionMaximaDias = ParametrosReservaService.ValoresPorDefecto.AntelacionMaximaDias,
                LimiteCancelacionMinutos = ParametrosReservaService.ValoresPorDefecto.LimiteCancelacionMinutos,
                VentanaCheckInMinutos = ParametrosReservaService.ValoresPorDefecto.VentanaCheckInMinutos,
                MaxReservasVigentesPorTelefono = ParametrosReservaService.ValoresPorDefecto.MaxReservasVigentesPorTelefono,
                MaxPersonasPorReserva = ParametrosReservaService.ValoresPorDefecto.MaxPersonasPorReserva
            });
        }

        var diasConHorario = await contexto.HorariosOperacion.IgnoreQueryFilters()
            .Select(h => h.DiaSemana)
            .ToListAsync(cancellationToken);

        foreach (var dia in Enum.GetValues<DayOfWeek>().Where(d => !diasConHorario.Contains(d)))
        {
            contexto.HorariosOperacion.Add(new HorarioOperacion
            {
                Id = Guid.NewGuid(),
                DiaSemana = dia,
                Abierto = true,
                HoraApertura = ParametrosReservaService.ValoresPorDefecto.HoraApertura,
                HoraCierre = ParametrosReservaService.ValoresPorDefecto.HoraCierre
            });
        }

        if (contexto.ChangeTracker.HasChanges())
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }
    }
}
