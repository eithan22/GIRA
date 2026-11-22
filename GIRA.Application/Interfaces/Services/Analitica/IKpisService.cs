using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

/// <summary>Consulta los indicadores de Analítica.</summary>
public interface IKpisService
{
    /// <summary>Calcula los indicadores para un intervalo inclusivo de fechas locales.</summary>
    Task<KpisDto> ConsultarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
