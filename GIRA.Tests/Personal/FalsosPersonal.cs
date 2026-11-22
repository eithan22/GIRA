using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Personal;
using GIRA.Application.Events.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Tests.Personal;

/// <summary>Repositorio de sugerencias en memoria para las pruebas de generación y resolución.</summary>
internal sealed class RepositorioSugerenciasFalso : ISugerenciaTurnoRepository
{
    public List<SugerenciaTurno> Sugerencias { get; } = [];

    public Task<SugerenciaTurno?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Sugerencias.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<SugerenciaTurno>> ListarAsync(EstadoSugerencia? estado, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SugerenciaTurno>>(
            Sugerencias.Where(s => estado is null || s.Estado == estado).ToList());

    public Task<IReadOnlyList<SugerenciaTurno>> ListarPendientesEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SugerenciaTurno>>(
            Sugerencias.Where(s => s.Estado == EstadoSugerencia.Pendiente && s.Fecha >= desde && s.Fecha <= hasta).ToList());

    public void Agregar(SugerenciaTurno sugerencia)
    {
        sugerencia.Id = Guid.NewGuid();
        Sugerencias.Add(sugerencia);
    }

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Repositorio de turnos en memoria con solo lo que usan la cobertura y las sugerencias.</summary>
internal sealed class RepositorioTurnosPrueba : ITurnoRepository
{
    public List<Turno> Turnos { get; } = [];

    public Task<IReadOnlyList<Turno>> ListarEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.Fecha >= desde && t.Fecha <= hasta).ToList());

    public Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IReadOnlyList<Turno>> ListarFuturosActivosAsync(Guid empleadoId, DateOnly desde, CancellationToken cancellationToken) => throw new NotImplementedException();

    public void Agregar(Turno turno) => Turnos.Add(turno);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Repositorio de requerimientos por cargo en memoria.</summary>
internal sealed class RepositorioRequerimientosFalso : IRequerimientoPersonalRepository
{
    public List<RequerimientoPersonal> Requerimientos { get; } = [];

    public Task<IReadOnlyList<RequerimientoPersonal>> ListarAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RequerimientoPersonal>>(Requerimientos.ToList());

    public Task<RequerimientoPersonal?> BuscarPorCargoAsync(Guid cargoId, CancellationToken cancellationToken) =>
        Task.FromResult(Requerimientos.FirstOrDefault(r => r.CargoId == cargoId));

    public void Agregar(RequerimientoPersonal requerimiento) => Requerimientos.Add(requerimiento);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Repositorio de cargos en memoria.</summary>
internal sealed class RepositorioCargosPrueba : ICargoRepository
{
    public List<Cargo> Cargos { get; } = [];

    public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Cargo>>(Cargos.ToList());

    public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Cargos.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) => throw new NotImplementedException();

    public void Agregar(Cargo cargo) => Cargos.Add(cargo);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Franjas de Reservas falsas. Solo se usa <c>ListarOperativasAsync</c>; el resto no se llama en estas pruebas.</summary>
internal sealed class FranjasPrueba : IFranjaService
{
    public List<FranjaOperativaDto> Franjas { get; } = [];

    public Task<OperationResult<IReadOnlyList<FranjaOperativaDto>>> ListarOperativasAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Success<IReadOnlyList<FranjaOperativaDto>>(Franjas.ToList()));

    public Task<OperationResult<PagedResult<FranjaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<OperationResult<FranjaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<OperationResult<FranjaDto>> CrearAsync(GuardarFranjaRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<OperationResult<FranjaDto>> ActualizarAsync(Guid id, GuardarFranjaRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<OperationResult<FranjaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken) => throw new NotImplementedException();
}

/// <summary>Publicador de eventos falso que guarda lo que se publica, para comprobarlo en las pruebas.</summary>
internal sealed class PublicadorPrueba : IPublicadorEventosSeguro
{
    public List<object> Publicados { get; } = [];

    public Task PublicarAsync<TEvento>(TEvento evento, string referencia, CancellationToken cancellationToken)
        where TEvento : MediatR.INotification
    {
        Publicados.Add(evento!);
        return Task.CompletedTask;
    }
}

/// <summary>Reloj con una hora fija, para que las pruebas no dependan del día en que se corren.</summary>
internal sealed class RelojPrueba(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
