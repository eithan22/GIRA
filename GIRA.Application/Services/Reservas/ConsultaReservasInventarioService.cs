using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Implementación de <see cref="IConsultaReservasInventario"/> (puerto de Inventario) con el repositorio de Reservas:
/// Inventario pregunta cuántas personas vienen con reserva sin conocer la entidad <c>Reserva</c>.
/// </summary>
public class ConsultaReservasInventarioService : IConsultaReservasInventario
{
    private readonly IReservaRepository _reservas;

    /// <summary>Crea el servicio con el repositorio de reservas.</summary>
    public ConsultaReservasInventarioService(IReservaRepository reservas)
    {
        _reservas = reservas;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<DateOnly, int>> PersonasConReservaPorFechaAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<DateOnly, int>();
        // shortcut: una consulta por día; la proyección cubre como máximo 14 días. Pasar a una consulta por rango si crece.
        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            var personas = (await _reservas.ListarVigentesPorFechaAsync(fecha, cancellationToken)).Sum(r => r.Personas);
            if (personas > 0)
            {
                resultado[fecha] = personas;
            }
        }

        return resultado;
    }
}
