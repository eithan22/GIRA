using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Common.Personal;

/// <summary>
/// Regla de cobertura del SRS (sección 5.4): el personal planificado de un cargo en una franja es la cantidad de
/// turnos Programados o Confirmados de ese cargo que cubren la franja completa. La usan la planificación (GI-301)
/// y la generación de sugerencias (GI-303).
/// </summary>
public static class CalculoCobertura
{
    /// <summary>Cuenta los turnos vigentes de un cargo que cubren la franja completa en una fecha.</summary>
    public static int Planificado(IEnumerable<Turno> turnos, DateOnly fecha, Guid cargoId, TimeOnly inicioFranja, TimeOnly finFranja) =>
        turnos.Count(t =>
            t.Fecha == fecha &&
            t.CargoId == cargoId &&
            (t.Estado is EstadoTurno.Programado or EstadoTurno.Confirmado) &&
            t.HoraInicio <= inicioFranja &&
            t.HoraFin >= finFranja);
}
