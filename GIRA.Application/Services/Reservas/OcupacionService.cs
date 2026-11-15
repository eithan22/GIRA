using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IOcupacionService"/>.</summary>
public class OcupacionService : IOcupacionService
{
    private readonly IOcupacionRepository _ocupacion;
    private readonly IValidator<ConsultarOcupacionRequest> _validador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con el repositorio de lectura, el validador y el reloj.</summary>
    public OcupacionService(
        IOcupacionRepository ocupacion, IValidator<ConsultarOcupacionRequest> validador, TimeProvider reloj)
    {
        _ocupacion = ocupacion;
        _validador = validador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<MapaOcupacionDto>> ObtenerMapaAsync(
        ConsultarOcupacionRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<MapaOcupacionDto>(ErrorType.Validation, validacion.ToString());
        }

        // Sin fecha: hoy en hora de Santo Domingo. Cualquier otra fecha válida (pasada o futura) se acepta.
        var fecha = ConsultarOcupacionRequestValidator.TryParseFecha(request.Fecha, out var indicada)
            ? indicada
            : _reloj.HoyLocal();
        ConsultarOcupacionRequestValidator.TryParseFranja(request.FranjaId, out var franjaId);

        var franja = await _ocupacion.ObtenerFranjaAsync(franjaId, cancellationToken);
        if (franja is null)
        {
            return OperationResult.Failure<MapaOcupacionDto>(ErrorType.NotFound, "No encontramos esa franja.");
        }

        var zonas = await _ocupacion.ListarZonasAsync(cancellationToken);
        var mesas = await _ocupacion.ListarMesasAsync(cancellationToken);
        var vigentes = await _ocupacion.ListarReservasVigentesAsync(fecha, franja.Id, cancellationToken);
        var proximas = await _ocupacion.ListarProximasReservasAsync(fecha, franja.HoraInicio, cancellationToken);

        // Si hubiera más de una reserva vigente en la misma mesa, gana EnCurso y, a igualdad, la de menor id:
        // el resultado no depende del orden en que la base devuelva las filas.
        var reservaPorMesa = vigentes
            .GroupBy(r => r.MesaId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.Estado == EstadoReserva.EnCurso).ThenBy(r => r.ReservaId).First());

        // "Próx. HH:mm": solo franjas posteriores a la del mapa (ya filtrado en el repositorio) que todavía no
        // han terminado (si el mapa es de hoy; cualquier franja de otro día cuenta como "no terminada"), la más
        // cercana por mesa. La visita sin reserva no cuenta aquí tampoco: no tiene hora de salida (GI-166).
        var esHoy = fecha == _reloj.HoyLocal();
        var ahoraLocal = _reloj.AhoraLocal();
        var proximaPorMesa = proximas
            .Where(p => !esHoy || fecha.ToDateTime(p.FranjaHoraFin) > ahoraLocal)
            .GroupBy(p => p.MesaId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.FranjaHoraInicio).First());

        var mesasPorZona = mesas.ToLookup(m => m.ZonaId);
        var zonasDto = new List<ZonaOcupacionDto>();
        int libres = 0, reservadas = 0, ocupadas = 0, inactivas = 0;

        foreach (var zona in zonas.OrderBy(z => z.Nombre, StringComparer.CurrentCultureIgnoreCase).ThenBy(z => z.Id))
        {
            var mesasDto = new List<MesaOcupacionDto>();
            foreach (var mesa in mesasPorZona[zona.Id].OrderBy(m => m.Numero).ThenBy(m => m.Id))
            {
                reservaPorMesa.TryGetValue(mesa.Id, out var reserva);
                proximaPorMesa.TryGetValue(mesa.Id, out var proxima);

                // La inactividad (de la mesa o de su zona) gana sobre la reserva; aun así se muestra la reserva.
                var estado = !mesa.Activa || !zona.Activa
                    ? EstadoMesaOcupacion.Inactiva
                    : reserva is null
                        ? EstadoMesaOcupacion.Libre
                        : reserva.Estado == EstadoReserva.EnCurso ? EstadoMesaOcupacion.Ocupada : EstadoMesaOcupacion.Reservada;

                switch (estado)
                {
                    case EstadoMesaOcupacion.Libre: libres++; break;
                    case EstadoMesaOcupacion.Reservada: reservadas++; break;
                    case EstadoMesaOcupacion.Ocupada: ocupadas++; break;
                    default: inactivas++; break;
                }

                mesasDto.Add(new MesaOcupacionDto(
                    mesa.Id, mesa.Numero, mesa.Capacidad, estado,
                    reserva?.ReservaId, reserva?.ClienteNombre, reserva?.Personas, reserva?.PersonasReales,
                    proxima?.FranjaHoraInicio, proxima?.Personas));
            }

            zonasDto.Add(new ZonaOcupacionDto(zona.Id, zona.Nombre, zona.Activa, mesasDto));
        }

        return OperationResult.Success(new MapaOcupacionDto(
            fecha, franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin,
            new ResumenOcupacionDto(libres, reservadas, ocupadas, inactivas), zonasDto));
    }
}
