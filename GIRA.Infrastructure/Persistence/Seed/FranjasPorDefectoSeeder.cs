using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Seed;

/// <summary>
/// Siembra las franjas de 2 horas por defecto, pero SOLO si la tabla de franjas está vacía (cuenta también las
/// borradas lógicamente, con <c>IgnoreQueryFilters</c>). Nunca modifica una franja existente: así no pisa lo que
/// el Gerente haya editado. Es idempotente y la pueden invocar el arranque en Development y las pruebas.
/// </summary>
public static class FranjasPorDefectoSeeder
{
    /// <summary>Inserta las cuatro franjas por defecto si todavía no hay ninguna.</summary>
    public static async Task SembrarAsync(GiraDbContext contexto, CancellationToken cancellationToken = default)
    {
        if (await contexto.FranjasHorarias.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var definicion in FranjasPorDefecto.Todas)
        {
            contexto.FranjasHorarias.Add(new FranjaHoraria
            {
                Id = definicion.Id,
                Nombre = definicion.Nombre,
                HoraInicio = definicion.HoraInicio,
                HoraFin = definicion.HoraFin,
                Activa = true
            });
        }

        await contexto.SaveChangesAsync(cancellationToken);
    }
}
