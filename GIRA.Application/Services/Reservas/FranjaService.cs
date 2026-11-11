using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Services.Reservas;

public class FranjaService : IFranjaService
{
    private readonly IFranjaGestionRepository _franjas;
    private readonly IReservasFuturasRepository _reservasFuturas;
    private readonly IValidator<GuardarFranjaRequest> _validador;
    private readonly IValidator<ListarSalonRequest> _validadorListado;
    private readonly TimeProvider _reloj;

    public FranjaService(
        IFranjaGestionRepository franjas,
        IReservasFuturasRepository reservasFuturas,
        IValidator<GuardarFranjaRequest> validador,
        IValidator<ListarSalonRequest> validadorListado,
        TimeProvider reloj)
    {
        _franjas = franjas;
        _reservasFuturas = reservasFuturas;
        _validador = validador;
        _validadorListado = validadorListado;
        _reloj = reloj;
    }

    public async Task<OperationResult<PagedResult<FranjaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<FranjaDto>>(ErrorType.Validation, validacion.ToString());
        }

        var pagina = await _franjas.ListarAsync(request.Activa, request.Page, request.PageSize, cancellationToken);
        var items = pagina.Items.Select(ToDto).ToList();

        return OperationResult.Success(new PagedResult<FranjaDto>(items, pagina.Total, pagina.Page, pagina.PageSize));
    }

    public async Task<OperationResult<FranjaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var franja = await _franjas.BuscarPorIdAsync(id, cancellationToken);
        return franja is null ? NoExiste() : OperationResult.Success(ToDto(franja));
    }

    public async Task<OperationResult<FranjaDto>> CrearAsync(GuardarFranjaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<FranjaDto>(ErrorType.Validation, validacion.ToString());
        }

        var nombre = request.Nombre.Trim();
        if (await _franjas.ExisteNombreAsync(nombre, null, cancellationToken))
        {
            return NombreDuplicado();
        }

        if (await _franjas.ExisteSolapamientoAsync(request.HoraInicio, request.HoraFin, null, cancellationToken))
        {
            return Solapada();
        }

        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            Activa = true
        };
        _franjas.Agregar(franja);

        return await GuardarAsync(franja, cancellationToken);
    }

    public async Task<OperationResult<FranjaDto>> ActualizarAsync(Guid id, GuardarFranjaRequest request, CancellationToken cancellationToken)
    {
        var franja = await _franjas.BuscarPorIdAsync(id, cancellationToken);
        if (franja is null)
        {
            return NoExiste();
        }

        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<FranjaDto>(ErrorType.Validation, validacion.ToString());
        }

        var nombre = request.Nombre.Trim();
        if (await _franjas.ExisteNombreAsync(nombre, id, cancellationToken))
        {
            return NombreDuplicado();
        }

        var cambiaHorario = request.HoraInicio != franja.HoraInicio || request.HoraFin != franja.HoraFin;
        if (cambiaHorario)
        {
            var reservas = await _reservasFuturas.ContarPorFranjaAsync(id, _reloj.HoyLocal(), cancellationToken);
            if (reservas > 0)
            {
                return OperationResult.Failure<FranjaDto>(
                    ErrorType.Conflict,
                    $"No se puede cambiar el horario: la franja tiene {reservas} reserva(s) futura(s) vigente(s). Reasígnalas antes.");
            }

            // Solo las franjas activas compiten por el horario; una inactiva se valida al reactivarla.
            if (franja.Activa
                && await _franjas.ExisteSolapamientoAsync(request.HoraInicio, request.HoraFin, id, cancellationToken))
            {
                return Solapada();
            }
        }

        franja.Nombre = nombre;
        franja.HoraInicio = request.HoraInicio;
        franja.HoraFin = request.HoraFin;

        return await GuardarAsync(franja, cancellationToken);
    }

    public async Task<OperationResult<FranjaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken)
    {
        var franja = await _franjas.BuscarPorIdAsync(id, cancellationToken);
        if (franja is null)
        {
            return NoExiste();
        }

        if (franja.Activa && !activa)
        {
            var reservas = await _reservasFuturas.ContarPorFranjaAsync(id, _reloj.HoyLocal(), cancellationToken);
            if (reservas > 0)
            {
                return OperationResult.Failure<FranjaDto>(
                    ErrorType.Conflict,
                    $"No se puede desactivar la franja: tiene {reservas} reserva(s) futura(s) vigente(s). Reasígnalas antes.");
            }
        }
        else if (!franja.Activa && activa
            && await _franjas.ExisteSolapamientoAsync(franja.HoraInicio, franja.HoraFin, id, cancellationToken))
        {
            return Solapada();
        }

        franja.Activa = activa;

        return await GuardarAsync(franja, cancellationToken);
    }

    private async Task<OperationResult<FranjaDto>> GuardarAsync(FranjaHoraria franja, CancellationToken cancellationToken)
    {
        try
        {
            await _franjas.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success(ToDto(franja));
        }
        catch (SalonDuplicadoException)
        {
            return NombreDuplicado();
        }
        catch (SalonConflictoConcurrenciaException)
        {
            return OperationResult.Failure<FranjaDto>(
                ErrorType.Conflict, "La franja cambió mientras la editabas; recárgala e inténtalo de nuevo.");
        }
    }

    private static OperationResult<FranjaDto> NoExiste() =>
        OperationResult.Failure<FranjaDto>(ErrorType.NotFound, "La franja no existe.");

    private static OperationResult<FranjaDto> NombreDuplicado() =>
        OperationResult.Failure<FranjaDto>(ErrorType.Conflict, "Ya existe una franja con ese nombre.");

    private static OperationResult<FranjaDto> Solapada() =>
        OperationResult.Failure<FranjaDto>(ErrorType.Conflict, "El horario se solapa con el de otra franja activa.");

    private static FranjaDto ToDto(FranjaHoraria franja) =>
        new(franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin, franja.Activa);
}