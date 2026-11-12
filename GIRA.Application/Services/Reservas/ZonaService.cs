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


/// <summary>Administración de zonas: CRUD, nombres únicos y bloqueo de desactivación con reservas futuras.</summary>
public class ZonaService : IZonaService
{
    private readonly IZonaRepository _zonas;
    private readonly IReservasFuturasRepository _reservasFuturas;
    private readonly IValidator<GuardarZonaRequest> _validador;
    private readonly IValidator<ListarSalonRequest> _validadorListado;
    private readonly TimeProvider _reloj;

    public ZonaService(
        IZonaRepository zonas,
        IReservasFuturasRepository reservasFuturas,
        IValidator<GuardarZonaRequest> validador,
        IValidator<ListarSalonRequest> validadorListado,
        TimeProvider reloj)
    {
        _zonas = zonas;
        _reservasFuturas = reservasFuturas;
        _validador = validador;
        _validadorListado = validadorListado;
        _reloj = reloj;
    }

    public async Task<OperationResult<PagedResult<ZonaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<ZonaDto>>(ErrorType.Validation, validacion.ToString());
        }

        var pagina = await _zonas.ListarAsync(request.Activa, request.Page, request.PageSize, cancellationToken);
        var items = pagina.Items.Select(ToDto).ToList();

        return OperationResult.Success(new PagedResult<ZonaDto>(items, pagina.Total, pagina.Page, pagina.PageSize));
    }

    public async Task<OperationResult<ZonaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var zona = await _zonas.BuscarPorIdAsync(id, cancellationToken);
        return zona is null ? NoExiste() : OperationResult.Success(ToDto(zona));
    }

    public async Task<OperationResult<ZonaDto>> CrearAsync(GuardarZonaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ZonaDto>(ErrorType.Validation, validacion.ToString());
        }

        var nombre = request.Nombre.Trim();
        if (await _zonas.ExisteNombreAsync(nombre, null, cancellationToken))
        {
            return NombreDuplicado();
        }

        var zona = new Zona { Id = Guid.NewGuid(), Nombre = nombre, Activa = true };
        _zonas.Agregar(zona);

        return await GuardarAsync(zona, cancellationToken);
    }

    public async Task<OperationResult<ZonaDto>> ActualizarAsync(Guid id, GuardarZonaRequest request, CancellationToken cancellationToken)
    {
        var zona = await _zonas.BuscarPorIdAsync(id, cancellationToken);
        if (zona is null)
        {
            return NoExiste();
        }

        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ZonaDto>(ErrorType.Validation, validacion.ToString());
        }

        var nombre = request.Nombre.Trim();
        if (await _zonas.ExisteNombreAsync(nombre, id, cancellationToken))
        {
            return NombreDuplicado();
        }

        zona.Nombre = nombre;

        return await GuardarAsync(zona, cancellationToken);
    }

    public async Task<OperationResult<ZonaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken)
    {
        var zona = await _zonas.BuscarPorIdAsync(id, cancellationToken);
        if (zona is null)
        {
            return NoExiste();
        }

        if (zona.Activa && !activa)
        {
            var reservas = await _reservasFuturas.ContarPorZonaAsync(id, _reloj.HoyLocal(), cancellationToken);
            if (reservas > 0)
            {
                return OperationResult.Failure<ZonaDto>(
                    ErrorType.Conflict,
                    $"No se puede desactivar la zona: sus mesas tienen {reservas} reserva(s) futura(s) vigente(s). Reasígnalas antes.");
            }
        }

        zona.Activa = activa;

        return await GuardarAsync(zona, cancellationToken);
    }

    private async Task<OperationResult<ZonaDto>> GuardarAsync(Zona zona, CancellationToken cancellationToken)
    {
        try
        {
            await _zonas.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success(ToDto(zona));
        }
        catch (SalonDuplicadoException)
        {
            return NombreDuplicado();
        }
        catch (SalonConflictoConcurrenciaException)
        {
            return OperationResult.Failure<ZonaDto>(
                ErrorType.Conflict, "La zona cambió mientras la editabas; recárgala e inténtalo de nuevo.");
        }
    }

    private static OperationResult<ZonaDto> NoExiste() =>
        OperationResult.Failure<ZonaDto>(ErrorType.NotFound, "La zona no existe.");

    private static OperationResult<ZonaDto> NombreDuplicado() =>
        OperationResult.Failure<ZonaDto>(ErrorType.Conflict, "Ya existe una zona con ese nombre.");

    private static ZonaDto ToDto(Zona zona) => new(zona.Id, zona.Nombre, zona.Activa);
}