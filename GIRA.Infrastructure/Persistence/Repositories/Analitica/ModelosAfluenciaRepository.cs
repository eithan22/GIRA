using System.Data;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class ModelosAfluenciaRepository(GiraDbContext contexto) : IModelosAfluenciaRepository
{
    public async Task<Guid> SolicitarEntrenamientoAsync(
        Guid usuarioId, DateTimeOffset solicitadoEn, CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuario solicitante es obligatorio.", nameof(usuarioId));

        await using var transaccion = await contexto.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var existente = await contexto.Set<EntrenamientoModelo>()
            .Where(e => e.Estado == EstadoEntrenamientoModelo.EnCola ||
                e.Estado == EstadoEntrenamientoModelo.EnCurso)
            .OrderBy(e => e.SolicitadoEn)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existente.HasValue)
        {
            await transaccion.CommitAsync(cancellationToken);
            return existente.Value;
        }

        var entrenamiento = new EntrenamientoModelo
        {
            Id = Guid.NewGuid(),
            SolicitadoPor = usuarioId,
            Estado = EstadoEntrenamientoModelo.EnCola,
            SolicitadoEn = solicitadoEn
        };
        contexto.Add(entrenamiento);
        await contexto.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return entrenamiento.Id;
    }

    public async Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken) =>
        await contexto.FranjasHorarias.AsNoTracking()
            .Where(f => f.Activa)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

    public async Task<EntrenamientoModelo?> IniciarSiguienteEntrenamientoAsync(
        DateTimeOffset iniciadoEn, CancellationToken cancellationToken)
    {
        var id = await contexto.Set<EntrenamientoModelo>().AsNoTracking()
            .Where(e => e.Estado == EstadoEntrenamientoModelo.EnCola ||
                (e.Estado == EstadoEntrenamientoModelo.EnCurso && e.IniciadoEn < iniciadoEn.AddHours(-24)))
            .OrderBy(e => e.SolicitadoEn)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue)
            return null;

        var actualizado = await contexto.Set<EntrenamientoModelo>()
            .Where(e => e.Id == id && (e.Estado == EstadoEntrenamientoModelo.EnCola ||
                (e.Estado == EstadoEntrenamientoModelo.EnCurso && e.IniciadoEn < iniciadoEn.AddHours(-24))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Estado, EstadoEntrenamientoModelo.EnCurso)
                .SetProperty(e => e.IniciadoEn, iniciadoEn)
                .SetProperty(e => e.Error, (string?)null), cancellationToken);
        if (actualizado == 0)
            return null;

        return await contexto.Set<EntrenamientoModelo>().AsNoTracking()
            .Include(e => e.Resultados)
            .SingleAsync(e => e.Id == id, cancellationToken);
    }

    public async Task ReencolarEntrenamientoAsync(Guid entrenamientoId, CancellationToken cancellationToken)
    {
        await contexto.Set<EntrenamientoModelo>()
            .Where(e => e.Id == entrenamientoId && e.Estado == EstadoEntrenamientoModelo.EnCurso)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Estado, EstadoEntrenamientoModelo.EnCola)
                .SetProperty(e => e.IniciadoEn, (DateTimeOffset?)null), cancellationToken);
    }

    public Task<EntrenamientoModelo?> ObtenerEntrenamientoAsync(
        Guid id, CancellationToken cancellationToken) =>
        contexto.Set<EntrenamientoModelo>().AsNoTracking()
            .Include(e => e.Resultados)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresFranjasAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await contexto.FranjasHorarias.IgnoreQueryFilters().AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nombre, cancellationToken);
    }

    public async Task<IReadOnlyList<ModeloAfluencia>> ObtenerModelosActivosAsync(
        CancellationToken cancellationToken) =>
        await contexto.Set<ModeloAfluencia>().AsNoTracking()
            .Where(m => m.Activo)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModeloAfluenciaDto>> ObtenerModelosAsync(
        CancellationToken cancellationToken) =>
        await (from modelo in contexto.Set<ModeloAfluencia>().AsNoTracking()
               join franja in contexto.FranjasHorarias.IgnoreQueryFilters().AsNoTracking()
                   on modelo.FranjaHorariaId equals franja.Id
               orderby modelo.CreadoEn descending
               select new ModeloAfluenciaDto(modelo.Id, modelo.FranjaHorariaId, franja.Nombre,
                   modelo.Version, modelo.EntrenamientoDesde, modelo.EntrenamientoHasta,
                   modelo.ValidacionDesde, modelo.ValidacionHasta, modelo.Mae, modelo.Rmse,
                   modelo.CreadoEn, modelo.Activo))
            .ToListAsync(cancellationToken);

    public async Task GuardarResultadosAsync(
        Guid entrenamientoId,
        IReadOnlyList<ResultadoEntrenamientoModelo> resultados,
        IReadOnlyList<ModeloAfluencia> modelos,
        DateTimeOffset finalizadoEn,
        CancellationToken cancellationToken)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken);
        var entrenamiento = await contexto.Set<EntrenamientoModelo>()
            .SingleAsync(e => e.Id == entrenamientoId &&
                e.Estado == EstadoEntrenamientoModelo.EnCurso, cancellationToken);
        var modelosQueSeActivan = modelos.Where(m => m.Activo).ToList();
        var franjas = modelosQueSeActivan.Select(m => m.FranjaHorariaId).ToArray();
        List<ModeloAfluencia> anteriores = franjas.Length == 0
            ? []
            : await contexto.Set<ModeloAfluencia>()
                .Where(m => m.Activo && franjas.Contains(m.FranjaHorariaId))
                .ToListAsync(cancellationToken);
        foreach (var anterior in anteriores)
            anterior.Activo = false;
        if (anteriores.Count > 0)
            await contexto.SaveChangesAsync(cancellationToken);

        contexto.Set<ModeloAfluencia>().AddRange(modelos);
        contexto.Set<ResultadoEntrenamientoModelo>().AddRange(resultados);
        foreach (var nuevo in modelosQueSeActivan)
        {
            var anterior = anteriores.SingleOrDefault(m => m.FranjaHorariaId == nuevo.FranjaHorariaId);
            contexto.Set<AuditoriaActivacionModelo>().Add(new AuditoriaActivacionModelo
            {
                Id = Guid.NewGuid(),
                ModeloAnteriorId = anterior?.Id,
                ModeloActivadoId = nuevo.Id,
                UsuarioId = entrenamiento.SolicitadoPor,
                ActivadoEn = nuevo.CreadoEn,
                EsRollback = false,
                Motivo = "Activación automática: el MAE de validación no supera el de referencia."
            });
        }

        entrenamiento.Estado = EstadoEntrenamientoModelo.Completado;
        entrenamiento.FinalizadoEn = finalizadoEn;
        await contexto.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }

    public async Task MarcarEntrenamientoFallidoAsync(
        Guid entrenamientoId, string error, DateTimeOffset finalizadoEn, CancellationToken cancellationToken)
    {
        var entrenamiento = await contexto.Set<EntrenamientoModelo>()
            .SingleAsync(e => e.Id == entrenamientoId, cancellationToken);
        entrenamiento.Estado = EstadoEntrenamientoModelo.Fallido;
        entrenamiento.Error = error;
        entrenamiento.FinalizadoEn = finalizadoEn;
        await contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResultadoActivacionModelo> ActivarModeloAsync(
        Guid modeloId,
        Guid usuarioId,
        string motivo,
        DateTimeOffset activadoEn,
        bool esRollback,
        CancellationToken cancellationToken)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var modelo = await contexto.Set<ModeloAfluencia>()
            .SingleOrDefaultAsync(m => m.Id == modeloId, cancellationToken);
        if (modelo is null)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return ResultadoActivacionModelo.NoEncontrado;
        }

        if (modelo.Activo)
        {
            await transaccion.CommitAsync(cancellationToken);
            return ResultadoActivacionModelo.YaActivo;
        }

        var anterior = await contexto.Set<ModeloAfluencia>()
            .SingleOrDefaultAsync(m => m.Activo && m.FranjaHorariaId == modelo.FranjaHorariaId,
                cancellationToken);
        if (anterior is null || modelo.CreadoEn >= anterior.CreadoEn)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return ResultadoActivacionModelo.NoEsRollback;
        }

        anterior.Activo = false;
        await contexto.SaveChangesAsync(cancellationToken);

        modelo.Activo = true;
        contexto.Set<AuditoriaActivacionModelo>().Add(new AuditoriaActivacionModelo
        {
            Id = Guid.NewGuid(),
            ModeloAnteriorId = anterior?.Id,
            ModeloActivadoId = modelo.Id,
            UsuarioId = usuarioId,
            ActivadoEn = activadoEn,
            EsRollback = esRollback,
            Motivo = motivo
        });
        await contexto.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return ResultadoActivacionModelo.Activado;
    }
}
