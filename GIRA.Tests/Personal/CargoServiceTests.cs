using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Exceptions.Personal;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas del servicio de cargos (GI-312): creación, edición, nombre repetido y choque con el índice único.
/// Usan un repositorio en memoria para no depender de la base de datos.
/// </summary>
public class CargoServiceTests
{
    [Fact]
    public async Task Crear_DatosValidos_GuardaElCargoConNombreSinEspacios()
    {
        var repo = new RepositorioCargosEnMemoria();
        var servicio = CrearServicio(repo);

        var resultado = await servicio.CrearAsync(new CargoRequest("  Mesero  ", "Atiende mesas"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Mesero", resultado.Value!.Nombre);
        Assert.Equal("Atiende mesas", resultado.Value.Descripcion);
        Assert.Single(repo.Cargos);
    }

    [Fact]
    public async Task Crear_DescripcionVacia_SeGuardaComoNula()
    {
        var repo = new RepositorioCargosEnMemoria();
        var servicio = CrearServicio(repo);

        var resultado = await servicio.CrearAsync(new CargoRequest("Cocinero", "   "), default);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.Descripcion);
    }

    [Fact]
    public async Task Crear_NombreVacio_FallaDeValidacionYNoGuarda()
    {
        var repo = new RepositorioCargosEnMemoria();
        var servicio = CrearServicio(repo);

        var resultado = await servicio.CrearAsync(new CargoRequest("   ", null), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Empty(repo.Cargos);
    }

    [Fact]
    public async Task Crear_NombreRepetido_DevuelveConflictoYNoGuarda()
    {
        var repo = new RepositorioCargosEnMemoria();
        repo.Cargos.Add(new Cargo { Id = Guid.NewGuid(), Nombre = "Mesero" });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.CrearAsync(new CargoRequest("Mesero", null), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Single(repo.Cargos);
    }

    [Fact]
    public async Task Crear_ChoqueConElIndiceUnico_DevuelveConflicto()
    {
        // Otra solicitud guardó el mismo nombre entre la revisión y el guardado.
        var repo = new RepositorioCargosEnMemoria { FallarAlGuardarPorNombre = true };
        var servicio = CrearServicio(repo);

        var resultado = await servicio.CrearAsync(new CargoRequest("Bartender", null), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_CargoInexistente_DevuelveNoEncontrado()
    {
        var repo = new RepositorioCargosEnMemoria();
        var servicio = CrearServicio(repo);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), new CargoRequest("Anfitrión", null), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_MismoNombreDelMismoCargo_NoEsConflicto()
    {
        var id = Guid.NewGuid();
        var repo = new RepositorioCargosEnMemoria();
        repo.Cargos.Add(new Cargo { Id = id, Nombre = "Cocinero" });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.ActualizarAsync(id, new CargoRequest("Cocinero", "Prepara platos"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Prepara platos", repo.Cargos.Single().Descripcion);
    }

    [Fact]
    public async Task Actualizar_NombreDeOtroCargo_DevuelveConflicto()
    {
        var id = Guid.NewGuid();
        var repo = new RepositorioCargosEnMemoria();
        repo.Cargos.Add(new Cargo { Id = id, Nombre = "Mesero" });
        repo.Cargos.Add(new Cargo { Id = Guid.NewGuid(), Nombre = "Cocinero" });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.ActualizarAsync(id, new CargoRequest("Cocinero", null), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("Mesero", repo.Cargos.Single(c => c.Id == id).Nombre);
    }

    [Fact]
    public async Task Listar_DevuelveLosCargosOrdenadosPorNombre()
    {
        var repo = new RepositorioCargosEnMemoria();
        repo.Cargos.Add(new Cargo { Id = Guid.NewGuid(), Nombre = "Mesero" });
        repo.Cargos.Add(new Cargo { Id = Guid.NewGuid(), Nombre = "Anfitrión" });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.ListarAsync(default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(["Anfitrión", "Mesero"], resultado.Value!.Select(c => c.Nombre).ToArray());
    }

    private static CargoService CrearServicio(ICargoRepository repo) =>
        new(repo, new CargoRequestValidator());

    /// <summary>Repositorio en memoria que imita el comportamiento que necesita el servicio.</summary>
    private sealed class RepositorioCargosEnMemoria : ICargoRepository
    {
        public List<Cargo> Cargos { get; } = [];

        /// <summary>Si es true, el guardado falla como lo haría el índice único.</summary>
        public bool FallarAlGuardarPorNombre { get; set; }

        public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Cargo>>(Cargos.OrderBy(c => c.Nombre).ToList());

        public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Cargos.FirstOrDefault(c => c.Id == id));

        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(Cargos.Any(c => c.Nombre == nombre && c.Id != excluirId));

        public void Agregar(Cargo cargo)
        {
            cargo.Id = Guid.NewGuid();
            Cargos.Add(cargo);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken)
        {
            if (FallarAlGuardarPorNombre)
            {
                throw new CargoNombreDuplicadoException("Choque simulado con el índice único.", new Exception());
            }

            return Task.CompletedTask;
        }
    }
}
