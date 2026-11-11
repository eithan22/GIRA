using GIRA.Application.Common;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IProcesoAutomaticoReservasService"/>.</summary>
/// <remarks>
/// Un scope (y un contexto de base de datos) por ciclo, con <see cref="IReservaRepository.Descartar"/> para
/// aislar a la reserva que falla: esta capa no depende del contenedor de dependencias y la lógica se
/// prueba construyéndola directamente. El <c>RowVersion</c> de la reserva evita que dos ejecuciones
/// simultáneas (por ejemplo, dos instancias de la API, o el personal y el proceso a la vez) procesen la
/// misma reserva dos veces.
/// </remarks>
public class ProcesoAutomaticoReservasService : IProcesoAutomaticoReservasService
{
    /// <summary>Máximo de reservas que se procesan por paso y por ciclo; el resto cae en los ciclos siguientes.</summary>
    public const int TopePorCiclo = 200;

    private readonly IReservaRepository _reservas;
    private readonly IParametrosReservaService _parametros;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con el repositorio, los parámetros, el publicador seguro y el reloj.</summary>
    public ProcesoAutomaticoReservasService(
        IReservaRepository reservas,
        IParametrosReservaService parametros,
        IPublicadorEventosSeguro publicador,
        TimeProvider reloj)
    {
        _reservas = reservas;
        _parametros = parametros;
        _publicador = publicador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<ResultadoProcesoReservas> EjecutarAsync(CancellationToken cancellationToken)
    {
        var noShows = await MarcarNoShowsAsync(cancellationToken);
        var cierres = await CompletarVencidasAsync(cancellationToken);

        return noShows.Combinar(cierres);
    }

    /// <inheritdoc />
    public async Task<ResultadoProcesoReservas> MarcarNoShowsAsync(CancellationToken cancellationToken)
    {
        var ahoraLocal = _reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);

        // Las más antiguas primero: las vencidas siempre quedan antes que las que aún no vencen,
        // así que el tope nunca deja una vencida detrás de una que todavía no lo está.
        var candidatas = await _reservas.ListarPorEstadoHastaAsync(
            EstadoReserva.Confirmada, hoy, TopePorCiclo, cancellationToken);
        var vencidas = candidatas
            .Where(c => ahoraLocal > c.Reserva.Fecha.ToDateTime(c.HoraInicio) + _parametros.ToleranciaNoShow)
            .ToList();

        var noAsistidas = 0;
        var omitidas = 0;
        var fallos = new List<FalloProcesoReserva>();

        foreach (var candidata in vencidas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reserva = candidata.Reserva;

            try
            {
                reserva.MarcarNoAsistio();
                await _reservas.GuardarCambiosAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ReservaConflictoConcurrenciaException)
            {
                // Otra ejecución ya la procesó (RowVersion): no se publica nada y no es un error.
                _reservas.Descartar(reserva);
                omitidas++;
                continue;
            }
            catch (Exception ex)
            {
                // La reserva sigue Confirmada en la base: se reintenta en el ciclo siguiente.
                _reservas.Descartar(reserva);
                fallos.Add(new FalloProcesoReserva(reserva.Id, ex));
                continue;
            }

            noAsistidas++;

            // Tras guardar, una sola vez; un handler que falla se registra y no revierte (SRS 3.3, regla 3).
            await _publicador.PublicarAsync(
                new ReservaNoAsistidaEvent(reserva.Id, reserva.Fecha, reserva.FranjaHorariaId, reserva.Personas),
                $"ReservaId={reserva.Id}",
                cancellationToken);
        }

        return new ResultadoProcesoReservas(
            noAsistidas, omitidas, fallos, TopeAlcanzado: vencidas.Count >= TopePorCiclo, TopePorCiclo);
    }

    /// <inheritdoc />
    public async Task<ResultadoProcesoReservas> CompletarVencidasAsync(CancellationToken cancellationToken)
    {
        var ahoraLocal = _reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);

        // Las más antiguas primero (las franjas de un mismo día no se solapan, así que ordenar por inicio
        // equivale a ordenar por fin): las terminadas siempre quedan antes que las que aún no terminan.
        var candidatas = await _reservas.ListarPorEstadoHastaAsync(
            EstadoReserva.EnCurso, hoy, TopePorCiclo, cancellationToken);
        var terminadas = candidatas
            .Where(c => ahoraLocal >= c.Reserva.Fecha.ToDateTime(c.HoraFin))
            .ToList();

        var completadas = 0;
        var omitidas = 0;
        var fallos = new List<FalloProcesoReserva>();

        foreach (var candidata in terminadas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reserva = candidata.Reserva;

            // EnCurso implica que el check-in guardó las personas reales; si faltaran sería una violación
            // de integridad: se registra como fallo de esa reserva y el resto del ciclo continúa.
            if (reserva.PersonasReales is not { } personasReales)
            {
                _reservas.Descartar(reserva);
                fallos.Add(new FalloProcesoReserva(
                    reserva.Id,
                    new InvalidOperationException($"La reserva {reserva.Codigo} está EnCurso sin personas reales registradas.")));
                continue;
            }

            try
            {
                reserva.Completar();
                await _reservas.GuardarCambiosAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ReservaConflictoConcurrenciaException)
            {
                // El personal (o otra ejecución) la completó a la vez: quien guardó primero publicó el evento.
                _reservas.Descartar(reserva);
                omitidas++;
                continue;
            }
            catch (Exception ex)
            {
                // La reserva sigue EnCurso en la base: se reintenta en el ciclo siguiente.
                _reservas.Descartar(reserva);
                fallos.Add(new FalloProcesoReserva(reserva.Id, ex));
                continue;
            }

            completadas++;

            // Tras guardar, una sola vez; un handler que falla se registra y no revierte (SRS 3.3, regla 3).
            await _publicador.PublicarAsync(
                new VisitaCompletadaEvent(reserva.Fecha, reserva.FranjaHorariaId, personasReales, ConReserva: true),
                $"ReservaId={reserva.Id}",
                cancellationToken);
        }

        return new ResultadoProcesoReservas(
            NoAsistidas: 0, omitidas, fallos, TopeAlcanzado: terminadas.Count >= TopePorCiclo, TopePorCiclo,
            Completadas: completadas);
    }
}
