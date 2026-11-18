using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Exceptions.Configuracion;

namespace GIRA.Application.Services.Configuracion;

/// <summary>Lee y guarda los parámetros de Inventario en la fila única de <see cref="ConfiguracionRestaurante"/> (GI-468).</summary>
public class ParametrosInventarioService : IParametrosInventarioService
{
    private readonly IConfiguracionRepository _repositorio;
    private readonly IValidator<ActualizarParametrosInventarioRequest> _validador;

    /// <summary>Crea el servicio.</summary>
    public ParametrosInventarioService(IConfiguracionRepository repositorio, IValidator<ActualizarParametrosInventarioRequest> validador)
    {
        _repositorio = repositorio;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<ParametrosInventarioDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var configuracion = (await _repositorio.LeerAsync(cancellationToken)).Configuracion ?? new ConfiguracionRestaurante();
        return ToDto(configuracion);
    }

    /// <inheritdoc />
    public async Task<OperationResult<ParametrosInventarioDto>> ActualizarAsync(
        ActualizarParametrosInventarioRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ParametrosInventarioDto>(ErrorType.Validation, validacion.ToString());
        }

        var configuracion = await _repositorio.BuscarConfiguracionAsync(cancellationToken);
        if (configuracion is null)
        {
            // Primera vez sin seeder: la fila única nace con las políticas de reserva recomendadas.
            configuracion = new ConfiguracionRestaurante
            {
                Id = ConfiguracionRestaurante.IdUnico,
                AntelacionMinimaMinutos = ParametrosReservaService.ValoresPorDefecto.AntelacionMinimaMinutos,
                AntelacionMaximaDias = ParametrosReservaService.ValoresPorDefecto.AntelacionMaximaDias,
                LimiteCancelacionMinutos = ParametrosReservaService.ValoresPorDefecto.LimiteCancelacionMinutos,
                VentanaCheckInMinutos = ParametrosReservaService.ValoresPorDefecto.VentanaCheckInMinutos,
                ToleranciaNoShowMinutos = ParametrosReservaService.ValoresPorDefecto.ToleranciaNoShowMinutos,
                MaxReservasVigentesPorTelefono = ParametrosReservaService.ValoresPorDefecto.MaxReservasVigentesPorTelefono,
                MaxPersonasPorReserva = ParametrosReservaService.ValoresPorDefecto.MaxPersonasPorReserva
            };
            _repositorio.AgregarConfiguracion(configuracion);
        }

        configuracion.DiasProyeccionInventario = request.DiasProyeccionInventario!.Value;
        configuracion.DiasAvisoVencimientoInventario = request.DiasAvisoVencimientoInventario!.Value;

        try
        {
            await _repositorio.GuardarCambiosAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is ConfiguracionConflictoConcurrenciaException or ConfiguracionDuplicadaException)
        {
            return OperationResult.Failure<ParametrosInventarioDto>(
                ErrorType.Conflict, "La configuración cambió mientras la editabas; recárgala e inténtalo de nuevo.");
        }

        return OperationResult.Success(ToDto(configuracion));
    }

    private static ParametrosInventarioDto ToDto(ConfiguracionRestaurante c) =>
        new(c.DiasProyeccionInventario, c.DiasAvisoVencimientoInventario);
}
