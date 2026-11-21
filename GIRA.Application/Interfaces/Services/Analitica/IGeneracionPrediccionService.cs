namespace GIRA.Application.Interfaces.Services.Analitica;

public interface IGeneracionPrediccionService
{
    Task EjecutarAsync(CancellationToken cancellationToken);
}
