using GIRA.Api.Common;
using GIRA.Api.Common.Reservas;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Reservas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Endpoints públicos y del personal del módulo Reservas.</summary>
[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private readonly IDisponibilidadService _disponibilidadService;
    private readonly IReservaService _reservaService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>Crea el controller con los servicios de disponibilidad, reservas y autorización.</summary>
    public ReservasController(
        IDisponibilidadService disponibilidadService, IReservaService reservaService,
        IAuthorizationService authorizationService)
    {
        _disponibilidadService = disponibilidadService;
        _reservaService = reservaService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// GI-157: franjas con al menos una mesa disponible para una fecha y cantidad de
    /// personas. Endpoint público, sin información de cuántas mesas quedan.
    /// </summary>
    [HttpGet("disponibilidad")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaConsulta)]
    [ProducesResponseType<IReadOnlyList<FranjaDisponibleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConsultarDisponibilidad( [FromQuery] DateOnly fecha, [FromQuery] int personas, CancellationToken cancellationToken)
    {
        var resultado = await _disponibilidadService.ConsultarAsync(
            new ConsultarDisponibilidadRequest(fecha, personas), cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-158/222/235: crea una reserva. Sin cabecera Authorization: invitado, Origen = Web,
    /// ignora cualquier Origen del cuerpo. Con token válido y política OperarReservas:
    /// personal, Origen debe ser Telefono o Presencial; el usuario que registra se toma del
    /// token (BaseEntity.CreatedBy), nunca del cuerpo. Devuelve 200 al crear en ambos casos,
    /// como el resto del proyecto.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaCreacion)]
    [ProducesResponseType<ReservaCreadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Crear(CrearReservaRequest request, CancellationToken cancellationToken)
    {
        // Sin cabecera: invitado. Nunca se lee request.Origen en esta rama, aunque el
        // cuerpo lo traiga — se ignora por completo.
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            var resultadoAnonimo = await _reservaService.CrearAsync(request, cancellationToken);
            return resultadoAnonimo.ToActionResult();
        }

        // Cabecera presente pero el token es inválido o venció: 401, nunca sigue como
        // anónimo (si siguiera, cualquier token roto equivaldría a crear como invitado).
        if (User.Identity?.IsAuthenticated != true)
        {
            return NoAutenticado();
        }

        var autorizacion = await _authorizationService.AuthorizeAsync(User, Politicas.OperarReservas);
        if (!autorizacion.Succeeded)
        {
            return AccesoDenegado();
        }

        if (request.Origen is not (OrigenReserva.Telefono or OrigenReserva.Presencial))
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation, "El origen es obligatorio y debe ser Telefono o Presencial.").ToActionResult();
        }

        // Token válido pero sin el claim UsuarioId: no debería pasar con nuestros propios
        // tokens, pero nunca se crea sin saber quién registra. 401, nunca 500.
        if (User.ObtenerUsuarioId() is null)
        {
            return NoAutenticado();
        }

        var resultado = await _reservaService.CrearComoPersonalAsync(request, request.Origen.Value, cancellationToken);
        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-164/197/199: agenda del personal, paginada (page/pageSize, máximo 100), filtrable por
    /// fecha, estado, franja y teléfono. Sin fecha ni teléfono muestra el día de hoy.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<PagedResult<ReservaListadoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar([FromQuery] ListarReservasRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.ListarAsync(request, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>GI-164/198/199: detalle de una reserva para el personal. 404 si no existe.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ReservaDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.ObtenerAsync(id, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-165/200/201: check-in de una reserva con las personas que llegaron. Solo desde 30
    /// minutos antes de la franja (parámetro configurable) y hasta que termina; 400 si las
    /// personas superan la capacidad de la mesa; 409 si el estado o la hora no lo permiten.
    /// Devuelve el detalle actualizado de la reserva.
    /// </summary>
    [HttpPatch("{id:guid}/check-in")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ReservaDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(Guid id, CheckInRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.CheckInAsync(id, request, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-160/186/187: modifica fecha, franja, personas y/o mesa de una reserva Confirmada
    /// (RN-RES-05). Hasta el inicio de la franja original. Revalida la disponibilidad como si fuera
    /// nueva y publica <c>ReservaModificadaEvent</c>. Devuelve el detalle actualizado.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ReservaDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modificar(Guid id, ModificarReservaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.ModificarAsync(id, request, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-223/237/238: cancela una reserva por el personal con motivo obligatorio (1 a 500
    /// caracteres). El personal puede cancelar hasta el inicio de la franja. 400 si el motivo no es
    /// válido; 409 si no está Confirmada o la franja ya empezó. Devuelve el detalle actualizado.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ReservaDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarPorPersonal(Guid id, CancelarPorPersonalRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.CancelarPorPersonalAsync(id, request, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-166/202/204: completa la visita (EnCurso pasa a Completada) y publica una sola vez
    /// <c>VisitaCompletadaEvent</c>. Sin ventana horaria. 409 si no está EnCurso. Devuelve el
    /// detalle actualizado de la reserva.
    /// </summary>
    [HttpPatch("{id:guid}/completar")]
    [Authorize(Policy = Politicas.OperarReservas)]
    [ProducesResponseType<ReservaDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Completar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.CompletarAsync(id, cancellationToken);

        return resultado.ToActionResult();
    }

    // Mismo ProblemDetails que RespuestaAutorizacionHandler para 401 — duplicado a propósito:
    // ese handler solo se activa en el middleware de autorización, que un endpoint
    // [AllowAnonymous] nunca atraviesa, así que no hay forma de reusarlo sin tocar Seguridad.
    private IActionResult NoAutenticado()
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "No autenticado",
            Detail = "Debes iniciar sesión para acceder a este recurso."
        })
        { StatusCode = StatusCodes.Status401Unauthorized };
    }

    // Mismo ProblemDetails que RespuestaAutorizacionHandler para 403 (ver nota de arriba).
    private static IActionResult AccesoDenegado()
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acceso denegado",
            Detail = "Tu rol no tiene permiso para realizar esta acción."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    /// <summary>
    /// GI-184/185: consulta una reserva por código y teléfono. Mismo 404 neutro tanto si el
    /// código no existe como si el teléfono no coincide, sin revelar cuál de los dos falló.
    /// El teléfono va en el cuerpo, nunca en la URL, para no quedar expuesto en logs ni en
    /// proxies intermedios.
    /// </summary>
    [HttpPost("consultar")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaConsultaCodigo)]
    [ProducesResponseType<ReservaConsultadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConsultarPorCodigo(ConsultarReservaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.ConsultarPorCodigoAsync(request, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-184/185: cancela una reserva por código y teléfono, sin motivo obligatorio. Mismo
    /// 404 neutro que la consulta si no coinciden; 409 si ya no se puede cancelar (estado o
    /// límite de anticipación). El teléfono va en el cuerpo, nunca en la URL, por la misma
    /// razón que la consulta.
    /// </summary>
    [HttpPatch("codigo/{codigo}/cancelar")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaCancelacion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CancelarPorCodigo(string codigo, CancelarReservaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.CancelarPorCodigoAsync(codigo, request, cancellationToken);

        return resultado.ToActionResult();
    }
}
