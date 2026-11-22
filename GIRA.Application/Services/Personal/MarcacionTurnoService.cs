using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Services.Personal;

public sealed class MarcacionTurnoService(
    IMarcacionTurnoRepository repositorio,
    TimeProvider reloj) : IMarcacionTurnoService
{
    public async Task<MarcacionTurnoDto?> ObtenerAbiertaAsync(
        Guid usuarioId, CancellationToken cancellationToken)
    {
        var turno = await repositorio.ObtenerAbiertaAsync(usuarioId, cancellationToken);
        return turno is null ? null : ToDto(turno, null);
    }

    public async Task<OperationResult<MarcacionTurnoDto>> MarcarEntradaAsync(
        Guid usuarioId, CancellationToken cancellationToken)
    {
        if (await repositorio.ObtenerAbiertaAsync(usuarioId, cancellationToken) is not null)
            return OperationResult.Failure<MarcacionTurnoDto>(ErrorType.Conflict, "Ya tienes un turno abierto.");

        var turno = new MarcacionTurno
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            InicioEn = reloj.GetUtcNow()
        };
        await repositorio.AgregarAsync(turno, cancellationToken);
        await repositorio.GuardarAsync(cancellationToken);
        return OperationResult.Success(ToDto(turno, null));
    }

    public async Task<OperationResult<MarcacionTurnoDto>> MarcarSalidaAsync(
        Guid usuarioId, int minutosPausa, CancellationToken cancellationToken)
    {
        var turno = await repositorio.ObtenerAbiertaAsync(usuarioId, cancellationToken);
        if (turno is null)
            return OperationResult.Failure<MarcacionTurnoDto>(ErrorType.NotFound, "No tienes un turno abierto.");

        var fin = reloj.GetUtcNow();
        var minutosTranscurridos = (fin - turno.InicioEn).TotalMinutes;
        if (minutosPausa < 0 || minutosPausa > minutosTranscurridos)
            return OperationResult.Failure<MarcacionTurnoDto>(
                ErrorType.Validation, "La pausa no puede superar el tiempo transcurrido del turno.");

        turno.FinEn = fin;
        turno.MinutosPausa = minutosPausa;
        await repositorio.GuardarAsync(cancellationToken);
        return OperationResult.Success(ToDto(turno, minutosTranscurridos));
    }

    private static MarcacionTurnoDto ToDto(MarcacionTurno turno, double? minutosTranscurridos) =>
        new(turno.Id, turno.InicioEn, turno.FinEn, turno.MinutosPausa,
            minutosTranscurridos is null
                ? null
                : (decimal)(minutosTranscurridos.Value - turno.MinutosPausa) / 60m);
}
