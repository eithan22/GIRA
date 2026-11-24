using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Tests.Personal;

/// <summary>Pruebas de la consulta y resolución de sugerencias (GI-304, RF-PER-10).</summary>
public class SugerenciaServiceTests
{
    private static readonly Guid Manana = Guid.NewGuid();
    private static readonly Guid Mesero = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();

    [Fact]
    public async Task Resolver_Pendiente_LaAceptaYRegistraQuienLaResolvio()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var sugerencia = Pendiente(EstadoSugerencia.Pendiente);
        sugerencias.Sugerencias.Add(sugerencia);
        var servicio = Crear(sugerencias);

        var resultado = await servicio.ResolverAsync(sugerencia.Id, EstadoSugerencia.Aceptada, Usuario, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoSugerencia.Aceptada, sugerencia.Estado);
        Assert.Equal(Usuario, sugerencia.ResueltaPor);
    }

    [Fact]
    public async Task Resolver_Pendiente_LaRechaza()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var sugerencia = Pendiente(EstadoSugerencia.Pendiente);
        sugerencias.Sugerencias.Add(sugerencia);
        var servicio = Crear(sugerencias);

        var resultado = await servicio.ResolverAsync(sugerencia.Id, EstadoSugerencia.Rechazada, Usuario, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoSugerencia.Rechazada, sugerencia.Estado);
    }

    [Fact]
    public async Task Resolver_YaResuelta_DevuelveConflicto()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var sugerencia = Pendiente(EstadoSugerencia.Aceptada);
        sugerencias.Sugerencias.Add(sugerencia);
        var servicio = Crear(sugerencias);

        var resultado = await servicio.ResolverAsync(sugerencia.Id, EstadoSugerencia.Rechazada, Usuario, default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(EstadoSugerencia.Aceptada, sugerencia.Estado);
    }

    [Fact]
    public async Task Resolver_ConEstadoVencida_DevuelveValidacion()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        var sugerencia = Pendiente(EstadoSugerencia.Pendiente);
        sugerencias.Sugerencias.Add(sugerencia);
        var servicio = Crear(sugerencias);

        var resultado = await servicio.ResolverAsync(sugerencia.Id, EstadoSugerencia.Vencida, Usuario, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal(EstadoSugerencia.Pendiente, sugerencia.Estado);
    }

    [Fact]
    public async Task Resolver_Inexistente_DevuelveNoEncontrado()
    {
        var servicio = Crear(new RepositorioSugerenciasFalso());

        var resultado = await servicio.ResolverAsync(Guid.NewGuid(), EstadoSugerencia.Aceptada, Usuario, default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Listar_FiltraPorEstadoYTraeNombres()
    {
        var sugerencias = new RepositorioSugerenciasFalso();
        sugerencias.Sugerencias.Add(Pendiente(EstadoSugerencia.Pendiente));
        sugerencias.Sugerencias.Add(Pendiente(EstadoSugerencia.Vencida));
        var servicio = Crear(sugerencias);

        var resultado = await servicio.ListarAsync(EstadoSugerencia.Pendiente, default);

        var lista = Assert.Single(resultado.Value!);
        Assert.Equal("Mañana", lista.FranjaNombre);
        Assert.Equal("Mesero", lista.CargoNombre);
    }

    private static SugerenciaTurno Pendiente(EstadoSugerencia estado) =>
        new()
        {
            Id = Guid.NewGuid(),
            Fecha = new DateOnly(2026, 12, 7),
            FranjaHorariaId = Manana,
            CargoId = Mesero,
            PersonalPlanificado = 0,
            PersonalRequerido = 2,
            Diferencia = 2,
            Estado = estado,
        };

    private static SugerenciaService Crear(RepositorioSugerenciasFalso sugerencias)
    {
        var cargos = new RepositorioCargosPrueba();
        cargos.Cargos.Add(new Cargo { Id = Mesero, Nombre = "Mesero" });
        var franjas = new FranjasPrueba();
        franjas.Franjas.Add(new FranjaOperativaDto(Manana, "Mañana", new TimeOnly(8, 0), new TimeOnly(10, 0), true));
        return new SugerenciaService(sugerencias, cargos, franjas);
    }
}
