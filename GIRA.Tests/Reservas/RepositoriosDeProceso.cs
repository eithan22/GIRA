using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Tests.Reservas;

/// <summary>Base para decorar un repositorio de reservas real y cambiarle solo lo que una prueba necesita.</summary>
class ReservaRepositoryDecorador : IReservaRepository
{
    protected readonly IReservaRepository Real;

    public ReservaRepositoryDecorador(IReservaRepository real) => Real = real;

    public virtual Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        Real.ListarVigentesPorFechaAsync(fecha, cancellationToken);
    public virtual Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        Real.ContarVigentesPorTelefonoAsync(telefono, cancellationToken);
    public virtual Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken) =>
        Real.BuscarPorCodigoYTelefonoAsync(codigo, telefono, cancellationToken);
    public virtual Task<PagedResult<ReservaListadoDto>> ListarAsync(
        DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono, int page, int pageSize, CancellationToken cancellationToken) =>
        Real.ListarAsync(fecha, estado, franjaId, telefono, page, pageSize, cancellationToken);
    public virtual Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Real.BuscarPorIdAsync(id, cancellationToken);
    public virtual Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken) =>
        Real.ObtenerDetalleAsync(id, cancellationToken);
    public virtual Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken) =>
        Real.ListarPorEstadoHastaAsync(estado, fechaMaxima, limite, cancellationToken);
    public virtual void Descartar(Reserva reserva) => Real.Descartar(reserva);
    public virtual Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken) =>
        Real.ExisteVigenteAsync(mesaId, fecha, franjaId, cancellationToken);
    public virtual Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        Real.ExisteCodigoAsync(codigo, cancellationToken);
    public virtual void Agregar(Reserva reserva) => Real.Agregar(reserva);
    public virtual Task GuardarCambiosAsync(CancellationToken cancellationToken) => Real.GuardarCambiosAsync(cancellationToken);
}

/// <summary>Hace fallar el guardado número <see cref="LlamadaQueFalla"/> (1, 2, 3…) con la excepción indicada.</summary>
class ReservaRepositoryQueFallaAlGuardar : ReservaRepositoryDecorador
{
    private int _llamadas;

    public ReservaRepositoryQueFallaAlGuardar(IReservaRepository real) : base(real) { }

    /// <summary>Número de guardado que falla; 0 = ninguno.</summary>
    public int LlamadaQueFalla { get; set; }

    /// <summary>Excepción que se lanza en ese guardado.</summary>
    public Exception? Excepcion { get; set; }

    public override Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        _llamadas++;
        if (_llamadas == LlamadaQueFalla)
        {
            throw Excepcion ?? new InvalidOperationException("Fallo simulado al guardar.");
        }

        return base.GuardarCambiosAsync(cancellationToken);
    }
}

/// <summary>Obliga a dos ejecuciones a haber leído las candidatas antes de que cualquiera guarde (carrera real).</summary>
class ReservaRepositoryConBarrera : ReservaRepositoryDecorador
{
    private readonly Barrier _barrera;

    public ReservaRepositoryConBarrera(IReservaRepository real, Barrier barrera) : base(real) => _barrera = barrera;

    public override async Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(
        EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken)
    {
        var candidatas = await base.ListarPorEstadoHastaAsync(estado, fechaMaxima, limite, cancellationToken);
        await Task.Run(() => _barrera.SignalAndWait(TimeSpan.FromSeconds(30)), cancellationToken);
        return candidatas;
    }
}
