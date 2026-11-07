namespace GIRA.Tests.Common;

/// <summary>TimeProvider de prueba que siempre devuelve el instante UTC configurado.</summary>
public class TimeProviderFalso : TimeProvider
{
    private readonly DateTimeOffset _ahora;

    /// <summary>Crea el reloj falso fijo en <paramref name="ahoraUtc"/>.</summary>
    public TimeProviderFalso(DateTimeOffset ahoraUtc)
    {
        _ahora = ahoraUtc;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _ahora;
}
