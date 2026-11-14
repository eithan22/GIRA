using FluentValidation;
using FluentValidation.Results;
using GIRA.Api.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GIRA.Api.Controllers.Configuracion;

/// <summary>
/// Configuración de reservas (GI-226, RF-CFG-01 a RF-CFG-03): políticas, horario de operación por día y días
/// cerrados. La lectura es para el personal operativo (<c>OperarReservas</c>); crear, editar y borrar solo para
/// Administrador y Gerente (<c>GestionarConfiguracion</c>). Los errores de formato o de rango salen como un
/// <c>ValidationProblem</c> estándar (400, con <c>errors</c> por campo en camelCase); las reglas que dependen del
/// estado (franjas, duplicados, concurrencia) salen como un ProblemDetails con un mensaje de texto.
/// </summary>
[ApiController]
[Route("api/configuracion/reservas")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class ConfiguracionReservasController : ControllerBase
{
    private readonly IConfiguracionReservasService _servicio;
    private readonly IAuthorizationService _autorizacion;
    private readonly IValidator<ActualizarParametrosRequest> _validadorParametros;
    private readonly IValidator<ActualizarHorarioRequest> _validadorHorario;
    private readonly IValidator<CrearDiaCerradoRequest> _validadorDiaCerrado;

    /// <summary>Crea el controlador con el servicio, la autorización y los validadores.</summary>
    public ConfiguracionReservasController(
        IConfiguracionReservasService servicio,
        IAuthorizationService autorizacion,
        IValidator<ActualizarParametrosRequest> validadorParametros,
        IValidator<ActualizarHorarioRequest> validadorHorario,
        IValidator<CrearDiaCerradoRequest> validadorDiaCerrado)
    {
        _servicio = servicio;
        _autorizacion = autorizacion;
        _validadorParametros = validadorParametros;
        _validadorHorario = validadorHorario;
        _validadorDiaCerrado = validadorDiaCerrado;
    }

    /// <summary>
    /// Políticas vigentes, sus valores por defecto (para «Restablecer valores recomendados») y la última
    /// actualización. <c>actualizadoPor</c> solo se incluye para quien tiene <c>GestionarConfiguracion</c>.
    /// </summary>
    [HttpGet("parametros")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ConfiguracionParametrosDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerParametros(CancellationToken cancellationToken)
    {
        var puedeGestionar = (await _autorizacion.AuthorizeAsync(User, Politicas.GestionarConfiguracion)).Succeeded;
        return (await _servicio.ObtenerParametrosAsync(puedeGestionar, cancellationToken)).ToActionResult();
    }

    /// <summary>
    /// Guarda las seis políticas. 400 (<c>ValidationProblem</c>) si algún valor no es válido; 409 si otra persona
    /// cambió la configuración a la vez. Las <c>advertencias</c> (por ejemplo, un grupo máximo mayor que la mesa más
    /// grande) no impiden guardar. Las reglas de creación rigen para reservas nuevas y no invalidan las existentes.
    /// </summary>
    [HttpPut("parametros")]
    [Authorize(Policy = Politicas.GestionarConfiguracion)]
    [ProducesResponseType<ParametrosGuardadosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarParametros(
        [FromBody] ActualizarParametrosRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorParametros.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return ProblemaDeValidacion(validacion);
        }

        return (await _servicio.ActualizarParametrosAsync(request, cancellationToken)).ToActionResult();
    }

    /// <summary>
    /// Horario de los 7 días, de lunes a domingo. <c>diaSemana</c> es el <c>DayOfWeek</c> de .NET:
    /// 0 = domingo, 1 = lunes … 6 = sábado.
    /// </summary>
    [HttpGet("horario")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<IReadOnlyList<HorarioDiaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarHorario(CancellationToken cancellationToken) =>
        (await _servicio.ListarHorarioAsync(cancellationToken)).ToActionResult();

    /// <summary>
    /// Guarda el horario de un día. <paramref name="dia"/> es el <c>DayOfWeek</c> de .NET: 0 = domingo,
    /// 1 = lunes … 6 = sábado (400 si está fuera de 0–6). Si el día queda abierto, ninguna franja activa puede quedar
    /// fuera del horario: 409 indicando la franja y el día. Al cerrar un día, <c>advertencias</c> avisa cuántas
    /// reservas vigentes hay en los próximos días de esa semana (no se cancelan).
    /// </summary>
    [HttpPut("horario/{dia:int}")]
    [Authorize(Policy = Politicas.GestionarConfiguracion)]
    [ProducesResponseType<HorarioGuardadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarHorario(
        int dia, [FromBody] ActualizarHorarioRequest request, CancellationToken cancellationToken)
    {
        if (dia is < 0 or > 6)
        {
            ModelState.AddModelError("dia", "El día debe estar entre 0 (domingo) y 6 (sábado).");
            return ValidationProblem(ModelState);
        }

        var validacion = await _validadorHorario.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return ProblemaDeValidacion(validacion);
        }

        return (await _servicio.ActualizarHorarioAsync((DayOfWeek)dia, request, cancellationToken)).ToActionResult();
    }

    /// <summary>Días cerrados de hoy en adelante, por fecha.</summary>
    [HttpGet("dias-cerrados")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<IReadOnlyList<DiaCerradoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarDiasCerrados(CancellationToken cancellationToken) =>
        (await _servicio.ListarDiasCerradosAsync(cancellationToken)).ToActionResult();

    /// <summary>
    /// Cierra una fecha. 400 (<c>ValidationProblem</c>) si la fecha es pasada o el motivo supera 200 caracteres;
    /// 409 si esa fecha ya está cerrada. <c>advertencias</c> avisa cuántas reservas vigentes hay ese día (no se cancelan).
    /// </summary>
    [HttpPost("dias-cerrados")]
    [Authorize(Policy = Politicas.GestionarConfiguracion)]
    [ProducesResponseType<DiaCerradoCreadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearDiaCerrado(
        [FromBody] CrearDiaCerradoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorDiaCerrado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return ProblemaDeValidacion(validacion);
        }

        return (await _servicio.CrearDiaCerradoAsync(request, cancellationToken)).ToActionResult();
    }

    /// <summary>Quita un día cerrado (borrado lógico). 204 si se quitó; 404 si no existe.</summary>
    [HttpDelete("dias-cerrados/{id:guid}")]
    [Authorize(Policy = Politicas.GestionarConfiguracion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarDiaCerrado(Guid id, CancellationToken cancellationToken) =>
        (await _servicio.EliminarDiaCerradoAsync(id, cancellationToken)).ToActionResult();

    // ValidationProblem estándar (400) con los errores por campo, en camelCase como el resto del JSON.
    private IActionResult ProblemaDeValidacion(ValidationResult validacion)
    {
        foreach (var error in validacion.Errors)
        {
            ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
