namespace GIRA.Tests.Common;

/// <summary>
/// <see cref="TimeProvider"/> de prueba con un reloj que solo avanza cuando se le ordena con
/// <see cref="Avanzar"/>, y con temporizadores propios: <c>Task.Delay(…, timeProvider)</c> y
/// <c>PeriodicTimer(…, timeProvider)</c> se disparan al avanzar el reloj, sin esperas reales.
/// </summary>
public sealed class TimeProviderAvanzable : TimeProvider
{
    private readonly object _candado = new();
    private readonly List<Temporizador> _temporizadores = [];
    private readonly SemaphoreSlim _creados = new(0);
    private DateTimeOffset _ahora;

    /// <summary>Crea el reloj con el instante UTC inicial.</summary>
    public TimeProviderAvanzable(DateTimeOffset inicioUtc) => _ahora = inicioUtc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_candado)
        {
            return _ahora;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var temporizador = new Temporizador(this, callback, state);
        temporizador.Change(dueTime, period);
        lock (_candado)
        {
            _temporizadores.Add(temporizador);
        }
        _creados.Release();
        return temporizador;
    }

    /// <summary>
    /// Espera la señal de que alguien creó un temporizador (un <c>Task.Delay</c> o un <c>PeriodicTimer</c>
    /// con este reloj). Es una espera por evento, no por tiempo: el plazo solo evita colgar una prueba que falla.
    /// </summary>
    public Task<bool> EsperarTemporizadorAsync() => _creados.WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>Avanza el reloj y dispara, en orden, los temporizadores que vencen en ese lapso.</summary>
    public void Avanzar(TimeSpan lapso)
    {
        DateTimeOffset objetivo;
        lock (_candado)
        {
            objetivo = _ahora + lapso;
        }

        while (true)
        {
            Temporizador? siguiente;
            lock (_candado)
            {
                siguiente = _temporizadores
                    .Where(t => t.Vence is not null && t.Vence <= objetivo)
                    .OrderBy(t => t.Vence)
                    .FirstOrDefault();
                if (siguiente is null)
                {
                    _ahora = objetivo;
                    return;
                }

                _ahora = siguiente.Vence!.Value;
                siguiente.Reprogramar();
            }

            siguiente.Disparar(); // fuera del candado: el callback puede volver a tocar el reloj
        }
    }

    private void Quitar(Temporizador temporizador)
    {
        lock (_candado)
        {
            _temporizadores.Remove(temporizador);
        }
    }

    private sealed class Temporizador : ITimer
    {
        private readonly TimeProviderAvanzable _reloj;
        private readonly TimerCallback _callback;
        private readonly object? _estado;
        private TimeSpan _periodo = Timeout.InfiniteTimeSpan;

        public Temporizador(TimeProviderAvanzable reloj, TimerCallback callback, object? estado)
        {
            _reloj = reloj;
            _callback = callback;
            _estado = estado;
        }

        public DateTimeOffset? Vence { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_reloj._candado)
            {
                _periodo = period;
                Vence = dueTime == Timeout.InfiniteTimeSpan ? null : _reloj._ahora + dueTime;
            }
            return true;
        }

        // Se llama con el candado del reloj tomado.
        public void Reprogramar() =>
            Vence = _periodo == Timeout.InfiniteTimeSpan || _periodo <= TimeSpan.Zero ? null : Vence + _periodo;

        public void Disparar() => _callback(_estado);

        public void Dispose() => _reloj.Quitar(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
