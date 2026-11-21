namespace GIRA.Application.Interfaces.Services.Analitica;

public static class ModeloSsaParametros
{
    public const int DiasEntrenamientoMinimos = 56;
    public const int HorizonteDias = 14;
    public const int DiasValidacion = 14;
}

public sealed record EntrenamientoSsa(byte[] ArchivoModelo, IReadOnlyList<float> Pronostico,
    IReadOnlyList<float> LimiteInferior, IReadOnlyList<float> LimiteSuperior);

public sealed record PronosticoSsa(IReadOnlyList<float> Pronostico,
    IReadOnlyList<float> LimiteInferior, IReadOnlyList<float> LimiteSuperior);

public interface IModeloSsaService
{
    EntrenamientoSsa Entrenar(IReadOnlyList<float> observaciones, CancellationToken cancellationToken);
    PronosticoSsa Predecir(byte[] archivoModelo, IReadOnlyList<float> nuevasObservaciones,
        CancellationToken cancellationToken);
}
