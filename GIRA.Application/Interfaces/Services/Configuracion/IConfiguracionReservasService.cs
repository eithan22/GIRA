using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>
/// Casos de uso de la configuración de reservas (GI-226, RF-CFG-01 a RF-CFG-03): políticas, horario por día y días
/// cerrados. La validación de formato y rangos la hace el controlador con FluentValidation; aquí van las reglas que
/// dependen del estado (franjas, duplicados, concurrencia). Cada escritura invalida la caché de parámetros.
/// </summary>
public interface IConfiguracionReservasService
{
    /// <summary>
    /// Políticas vigentes y valores por defecto. <paramref name="incluirAuditoria"/> añade quién las modificó
    /// por última vez (solo para quien tiene <c>GestionarConfiguracion</c>).
    /// </summary>
    Task<OperationResult<ConfiguracionParametrosDto>> ObtenerParametrosAsync(bool incluirAuditoria, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda las políticas (crea la fila si todavía no existe). Avisa, sin impedir guardar, si el máximo de
    /// personas supera la mesa activa más grande. ErrorType.Conflict si hubo una modificación concurrente.
    /// </summary>
    Task<OperationResult<ParametrosGuardadosDto>> ActualizarParametrosAsync(ActualizarParametrosRequest request, CancellationToken cancellationToken);

    /// <summary>Horario de los 7 días, de lunes a domingo (los que no tienen fila salen con el valor por defecto).</summary>
    Task<OperationResult<IReadOnlyList<HorarioDiaDto>>> ListarHorarioAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Guarda el horario de un día (<c>DayOfWeek</c>: 0 = domingo … 6 = sábado). Si el día queda abierto, ninguna
    /// franja activa puede quedar fuera del horario (ErrorType.Conflict indicando franja y día). Al cerrar un día
    /// avisa cuántas reservas vigentes futuras hay en ese día de la semana.
    /// </summary>
    Task<OperationResult<HorarioGuardadoDto>> ActualizarHorarioAsync(DayOfWeek dia, ActualizarHorarioRequest request, CancellationToken cancellationToken);

    /// <summary>Días cerrados de hoy en adelante, por fecha.</summary>
    Task<OperationResult<IReadOnlyList<DiaCerradoDto>>> ListarDiasCerradosAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Cierra una fecha. ErrorType.Conflict si ya está cerrada. Avisa cuántas reservas vigentes hay ese día;
    /// no se cancelan (las reglas de creación no invalidan las reservas existentes).
    /// </summary>
    Task<OperationResult<DiaCerradoCreadoDto>> CrearDiaCerradoAsync(CrearDiaCerradoRequest request, CancellationToken cancellationToken);

    /// <summary>Quita un día cerrado. ErrorType.NotFound si no existe.</summary>
    Task<OperationResult> EliminarDiaCerradoAsync(Guid id, CancellationToken cancellationToken);
}
