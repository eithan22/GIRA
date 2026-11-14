using System.Text.Json.Serialization;

namespace GIRA.Application.DTOs.Configuracion;

/// <summary>Las seis políticas de reserva editables, en las unidades guardadas (minutos, días o cantidades).</summary>
/// <param name="AntelacionMinimaMinutos">Antelación mínima para crear una reserva, en minutos.</param>
/// <param name="AntelacionMaximaDias">Antelación máxima para crear una reserva, en días.</param>
/// <param name="LimiteCancelacionMinutos">Minutos antes del inicio de la franja hasta los que el cliente puede cancelar.</param>
/// <param name="VentanaCheckInMinutos">Minutos antes del inicio de la franja desde los que se permite el check-in.</param>
/// <param name="MaxReservasVigentesPorTelefono">Máximo de reservas vigentes con un mismo teléfono.</param>
/// <param name="MaxPersonasPorReserva">Tamaño máximo de grupo por reserva.</param>
public sealed record ParametrosEditablesDto(
    int AntelacionMinimaMinutos,
    int AntelacionMaximaDias,
    int LimiteCancelacionMinutos,
    int VentanaCheckInMinutos,
    int MaxReservasVigentesPorTelefono,
    int MaxPersonasPorReserva);

/// <summary>Cuerpo de <c>PUT /api/configuracion/reservas/parametros</c>: las seis políticas.</summary>
public sealed record ActualizarParametrosRequest(
    int AntelacionMinimaMinutos,
    int AntelacionMaximaDias,
    int LimiteCancelacionMinutos,
    int VentanaCheckInMinutos,
    int MaxReservasVigentesPorTelefono,
    int MaxPersonasPorReserva);

/// <summary>Políticas vigentes, sus valores por defecto y cuándo se actualizaron por última vez.</summary>
/// <param name="Parametros">Valores vigentes (los por defecto si todavía no se guardó nada).</param>
/// <param name="ValoresPorDefecto">Valores recomendados, para «Restablecer valores recomendados» en la pantalla.</param>
/// <param name="ActualizadoEn">Última modificación (UTC); null si nunca se modificó.</param>
/// <param name="ActualizadoPor">
/// Usuario que modificó por última vez. Solo se informa a quien tiene <c>GestionarConfiguracion</c>; para el
/// resto del personal el campo no aparece en la respuesta.
/// </param>
public sealed record ConfiguracionParametrosDto(
    ParametrosEditablesDto Parametros,
    ParametrosEditablesDto ValoresPorDefecto,
    DateTime? ActualizadoEn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ActualizadoPor);

/// <summary>Resultado de guardar las políticas, con avisos que no impiden guardar.</summary>
public sealed record ParametrosGuardadosDto(ConfiguracionParametrosDto Configuracion, IReadOnlyList<string> Advertencias);

/// <summary>Horario de un día de la semana.</summary>
/// <param name="DiaSemana">Día de la semana como <c>DayOfWeek</c> de .NET: 0 = domingo, 1 = lunes … 6 = sábado.</param>
/// <param name="NombreDia">Nombre del día en español, solo informativo.</param>
/// <param name="Abierto">Si el restaurante abre ese día.</param>
/// <param name="HoraApertura">Hora de apertura (hora local).</param>
/// <param name="HoraCierre">Hora de cierre (hora local).</param>
public sealed record HorarioDiaDto(int DiaSemana, string NombreDia, bool Abierto, TimeOnly HoraApertura, TimeOnly HoraCierre);

/// <summary>Cuerpo de <c>PUT /api/configuracion/reservas/horario/{dia}</c>.</summary>
public sealed record ActualizarHorarioRequest(bool Abierto, TimeOnly HoraApertura, TimeOnly HoraCierre);

/// <summary>Resultado de guardar un horario, con avisos que no impiden guardar.</summary>
public sealed record HorarioGuardadoDto(HorarioDiaDto Horario, IReadOnlyList<string> Advertencias);

/// <summary>Fecha cerrada del restaurante.</summary>
public sealed record DiaCerradoDto(Guid Id, DateOnly Fecha, string? Motivo);

/// <summary>Cuerpo de <c>POST /api/configuracion/reservas/dias-cerrados</c>.</summary>
public sealed record CrearDiaCerradoRequest(DateOnly Fecha, string? Motivo);

/// <summary>Resultado de cerrar una fecha, con avisos que no impiden guardar.</summary>
public sealed record DiaCerradoCreadoDto(DiaCerradoDto DiaCerrado, IReadOnlyList<string> Advertencias);
