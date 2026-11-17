namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>
/// Bloqueo que serializa la creación de reservas de un mismo teléfono (ver
/// <see cref="IReservaRepository.BloquearCreacionPorTelefonoAsync"/>). Mientras se mantiene, ninguna otra creación
/// del mismo teléfono puede contar sus reservas vigentes ni guardar la suya.
/// </summary>
public interface IBloqueoCreacionReserva : IAsyncDisposable
{
    /// <summary>
    /// Confirma lo guardado y libera el bloqueo. Se llama justo después del último guardado y ANTES de publicar
    /// eventos. Si no se llama (retorno temprano o excepción), <c>DisposeAsync</c> descarta lo pendiente y libera.
    /// </summary>
    Task ConfirmarAsync(CancellationToken cancellationToken);
}

/// <summary>Bloqueo sin efecto: proveedores sin transacciones ni bloqueos de aplicación (bases en memoria de las pruebas).</summary>
public sealed class BloqueoCreacionReservaNulo : IBloqueoCreacionReserva
{
    /// <summary>Instancia única; no guarda estado.</summary>
    public static readonly BloqueoCreacionReservaNulo Instancia = new();

    private BloqueoCreacionReservaNulo()
    {
    }

    /// <inheritdoc />
    public Task ConfirmarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
