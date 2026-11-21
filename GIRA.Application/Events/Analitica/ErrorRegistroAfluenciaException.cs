namespace GIRA.Application.Events.Analitica;

public sealed class ErrorRegistroAfluenciaException(Exception innerException)
    : Exception("No se pudo registrar el evento de afluencia.", innerException)
{
}
