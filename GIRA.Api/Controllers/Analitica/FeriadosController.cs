using FluentValidation;
using GIRA.Api.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GIRA.Api.Controllers.Analitica;

[ApiController]
[Route("api/feriados")]
[Authorize(Policy = Politicas.GestionarConfiguracion)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class FeriadosController(
    IConfiguracionReservasService configuracion,
    IValidator<CrearDiaCerradoRequest> validador) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DiaCerradoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        (await configuracion.ListarDiasCerradosAsync(cancellationToken)).ToActionResult();

    [HttpPost]
    [ProducesResponseType<DiaCerradoCreadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearDiaCerradoRequest solicitud, CancellationToken cancellationToken)
    {
        var validacion = await validador.ValidateAsync(solicitud, cancellationToken);
        if (!validacion.IsValid)
        {
            foreach (var error in validacion.Errors)
                ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        return (await configuracion.CrearDiaCerradoAsync(solicitud, cancellationToken)).ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken) =>
        (await configuracion.EliminarDiaCerradoAsync(id, cancellationToken)).ToActionResult();
}
