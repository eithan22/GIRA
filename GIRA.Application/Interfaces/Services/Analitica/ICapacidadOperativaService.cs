namespace GIRA.Application.Interfaces.Services.Analitica;

public interface ICapacidadOperativaService
{
    Task CapturarAntesDeAperturaAsync(
        DateOnly fecha, TimeOnly horaLocal, CancellationToken cancellationToken);
}
