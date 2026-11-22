using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class PrediccionAfluenciaRepository(GiraDbContext contexto) : IPrediccionAfluenciaRepository
{
    public async Task<IReadOnlyList<PrediccionAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, Guid? franjaId, CancellationToken cancellationToken)
    {
        return await (from prediccion in contexto.Set<PrediccionAfluencia>().AsNoTracking()
                      join franja in contexto.FranjasHorarias.IgnoreQueryFilters().AsNoTracking()
                          on prediccion.FranjaHorariaId equals franja.Id
                      where prediccion.Vigente && prediccion.Fecha >= desde && prediccion.Fecha <= hasta
                          && (franjaId == null || prediccion.FranjaHorariaId == franjaId)
                      orderby prediccion.Fecha, franja.HoraInicio, franja.Id
                      select new PrediccionAfluenciaDto(
                          prediccion.Fecha, prediccion.FranjaHorariaId, franja.Nombre,
                          prediccion.AfluenciaEstimada, prediccion.LimiteInferior, prediccion.LimiteSuperior,
                          prediccion.BajaConfianza, prediccion.GeneradaEn, prediccion.VersionModelo))
            .ToListAsync(cancellationToken);
    }
}
