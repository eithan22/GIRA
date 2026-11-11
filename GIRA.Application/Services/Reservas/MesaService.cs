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

public class MesaService : IMesaService
{
    private readonly IMesaGestionRepository _mesas;
    private readonly IZonaRepository _zonas;
    private readonly IReservasFuturasRepository _reservasFuturas;
    private readonly IValidator<GuardarMesaRequest> _validador;
    private readonly IValidator<ListarSalonRequest> _validadorListado;
    private readonly TimeProvider _reloj;

    public MesaService(
        IMesaGestionRepository mesas,
        IZonaRepository zonas,
        IReservasFuturasRepository reservasFuturas,
        IValidator<GuardarMesaRequest> validador,
        IValidator<ListarSalonRequest> validadorListado,
        TimeProvider reloj)
    {
        _mesas = mesas;
        _zonas = zonas;
        _reservasFuturas = reservasFuturas;
        _validador = validador;
        _validadorListado = validadorListado;
        _reloj = reloj;
    }

    public async Task<OperationResult<PagedResult<MesaDto>>> ListarAsync(ListarMesasRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<MesaDto>>(ErrorType.Validation, validacion.ToString());
        }

        var pagina = await _mesas.ListarAsync(request.ZonaId, request.Activa, request.Page, request.PageSize, cancellationToken);
        var zonaIds = pagina.Items.Select(m => m.ZonaId).Distinct().ToList();
        var nombres = await _zonas.ObtenerNombresAsync(zonaIds, cancellationToken);
        var items = pagina.Items.Select(m => ToDto(m, NombreDe(nombres, m.ZonaId))).ToList();

        return OperationResult.Success(new PagedResult<MesaDto>(items, pagina.Total, pagina.Page, pagina.PageSize));
    }

    public async Task<OperationResult<MesaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var mesa = await _mesas.BuscarPorIdAsync(id, cancellationToken);
        if (mesa is null)
        {
            return NoExiste();
        }

        return OperationResult.Success(ToDto(mesa, await NombreZonaAsync(mesa.ZonaId, cancellationToken)));
    }

    public async Task<OperationResult<MesaDto>> CrearAsync(GuardarMesaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<MesaDto>(ErrorType.Validation, validacion.ToString());
        }

        var zona = await _zonas.BuscarPorIdAsync(request.ZonaId, cancellationToken);
        var errorZona = ValidarZona(zona);
        if (errorZona is not null)
        {
            return errorZona;
        }

        if (await _mesas.ExisteNumeroAsync(request.Numero, null, cancellationToken))
        {
            return NumeroDuplicado(request.Numero);
        }

        var mesa = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = request.Numero,
            Capacidad = request.Capacidad,
            ZonaId = request.ZonaId,
            Activa = true
        };
        _mesas.Agregar(mesa);

        return await GuardarAsync(mesa, zona!.Nombre, cancellationToken);
    }

    public async Task<OperationResult<MesaDto>> ActualizarAsync(Guid id, GuardarMesaRequest request, CancellationToken cancellationToken)
    {
        var mesa = await _mesas.BuscarPorIdAsync(id, cancellationToken);
        if (mesa is null)
        {
            return NoExiste();
        }

        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<MesaDto>(ErrorType.Validation, validacion.ToString());
        }

        var zona = await _zonas.BuscarPorIdAsync(request.ZonaId, cancellationToken);
        if (request.ZonaId != mesa.ZonaId)
        {
            // Solo se exige zona activa al MOVER la mesa; editar una mesa de una zona ya desactivada es válido.
            var errorZona = ValidarZona(zona);
            if (errorZona is not null)
            {
                return errorZona;
            }
        }
        else if (zona is null)
        {
            return OperationResult.Failure<MesaDto>(ErrorType.Validation, "La zona indicada no existe.");
        }

        if (await _mesas.ExisteNumeroAsync(request.Numero, id, cancellationToken))
        {
            return NumeroDuplicado(request.Numero);
        }

        if (request.Capacidad < mesa.Capacidad)
        {
            var maximo = await _reservasFuturas.MaximoPersonasPorMesaAsync(id, _reloj.HoyLocal(), cancellationToken);
            if (maximo > request.Capacidad)
            {
                return OperationResult.Failure<MesaDto>(
                    ErrorType.Conflict,
                    $"No se puede reducir la capacidad a {request.Capacidad}: hay una reserva futura de {maximo} personas en esta mesa.");
            }
        }

        mesa.Numero = request.Numero;
        mesa.Capacidad = request.Capacidad;
        mesa.ZonaId = request.ZonaId;

        return await GuardarAsync(mesa, zona!.Nombre, cancellationToken);
    }

    public async Task<OperationResult<MesaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken)
    {
        var mesa = await _mesas.BuscarPorIdAsync(id, cancellationToken);
        if (mesa is null)
        {
            return NoExiste();
        }

        if (mesa.Activa && !activa)
        {
            var reservas = await _reservasFuturas.ContarPorMesaAsync(id, _reloj.HoyLocal(), cancellationToken);
            if (reservas > 0)
            {
                return OperationResult.Failure<MesaDto>(
                    ErrorType.Conflict,
                    $"No se puede desactivar la mesa: tiene {reservas} reserva(s) futura(s) vigente(s). Reasígnalas antes.");
            }
        }

        mesa.Activa = activa;

        return await GuardarAsync(mesa, await NombreZonaAsync(mesa.ZonaId, cancellationToken), cancellationToken);
    }

    private static OperationResult<MesaDto>? ValidarZona(Zona? zona)
    {
        if (zona is null)
        {
            return OperationResult.Failure<MesaDto>(ErrorType.Validation, "La zona indicada no existe.");
        }

        return zona.Activa
            ? null
            : OperationResult.Failure<MesaDto>(ErrorType.Conflict, "La zona está desactivada; actívala o elige otra.");
    }

    private async Task<OperationResult<MesaDto>> GuardarAsync(Mesa mesa, string zonaNombre, CancellationToken cancellationToken)
    {
        try
        {
            await _mesas.GuardarCambiosAsync(cancellationToken);
            return OperationResult.Success(ToDto(mesa, zonaNombre));
        }
        catch (SalonDuplicadoException)
        {
            return NumeroDuplicado(mesa.Numero);
        }
        catch (SalonConflictoConcurrenciaException)
        {
            return OperationResult.Failure<MesaDto>(
                ErrorType.Conflict, "La mesa cambió mientras la editabas; recárgala e inténtalo de nuevo.");
        }
    }

    private async Task<string> NombreZonaAsync(Guid zonaId, CancellationToken cancellationToken)
    {
        var nombres = await _zonas.ObtenerNombresAsync(new[] { zonaId }, cancellationToken);
        return NombreDe(nombres, zonaId);
    }

    private static string NombreDe(IReadOnlyDictionary<Guid, string> nombres, Guid zonaId) =>
        nombres.TryGetValue(zonaId, out var nombre) ? nombre : string.Empty;

    private static OperationResult<MesaDto> NoExiste() =>
        OperationResult.Failure<MesaDto>(ErrorType.NotFound, "La mesa no existe.");

    private static OperationResult<MesaDto> NumeroDuplicado(int numero) =>
        OperationResult.Failure<MesaDto>(ErrorType.Conflict, $"Ya existe la mesa {numero}.");

    private static MesaDto ToDto(Mesa mesa, string zonaNombre) =>
        new(mesa.Id, mesa.Numero, mesa.Capacidad, mesa.ZonaId, zonaNombre, mesa.Activa);
}