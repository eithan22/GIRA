using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Services.Analitica;

public sealed class CapacidadOperativaService(
    ICapacidadOperativaRepository repositorio,
    IParametrosReservaService parametros,
    TimeProvider reloj) : ICapacidadOperativaService
{
    public async Task CapturarAntesDeAperturaAsync(
        DateOnly fecha, TimeOnly horaLocal, CancellationToken cancellationToken)
    {
        if (await repositorio.ExisteAsync(fecha, cancellationToken))
            return;

        var configuracion = await parametros.ObtenerAsync(cancellationToken);
        var capacidad = 0;
        if (configuracion.EstaAbierto(fecha))
        {
            if (!configuracion.Horarios.TryGetValue(fecha.DayOfWeek, out var horario) ||
                horaLocal >= horario.HoraApertura)
                return;

            capacidad = await repositorio.CalcularCapacidadActualAsync(cancellationToken);
        }

        await repositorio.GuardarAsync(new CapacidadOperativaDiaria
        {
            Id = Guid.NewGuid(),
            Fecha = fecha,
            CapacidadTotal = capacidad,
            CapturadaEn = reloj.GetUtcNow()
        }, cancellationToken);
    }
}
