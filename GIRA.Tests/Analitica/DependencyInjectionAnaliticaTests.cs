using GIRA.Application;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Services.Analitica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GIRA.Tests.Analitica;

public sealed class DependencyInjectionAnaliticaTests
{
    [Fact]
    public void RegistraServiciosRepositoriosYProcesosUnaSolaVez()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GiraDb"] = "Server=(localdb)\\MSSQLLocalDB;Database=GiraDiTest;Trusted_Connection=True;TrustServerCertificate=True",
            ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
            ["Jwt:Issuer"] = "GIRA.Api",
            ["Jwt:Audience"] = "GIRA.Frontend",
            ["Reservas:ProcesoAutomatico:Habilitado"] = "false",
            ["Inventario:ProcesoVencimientos:Habilitado"] = "false",
            ["Analitica:Generacion:Habilitada"] = "true",
            ["Analitica:Entrenamiento:Habilitada"] = "true",
            ["Analitica:Recomendaciones:Habilitada"] = "true",
            ["Analitica:Recomendaciones:PorcentajeAumentoDemanda"] = "30",
            ["Analitica:Recomendaciones:PorcentajeNoShow"] = "15",
            ["Analitica:Recomendaciones:AlertasMensuales"] = "2"
        }).Build();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddApplication();
        servicios.AddInfrastructure(configuracion);
        servicios.AddSingleton(TimeProvider.System);

        using var proveedor = servicios.BuildServiceProvider(validateScopes: true);
        using var scope = proveedor.CreateScope();
        var tipos = new[]
        {
            typeof(IHistorialAfluenciaService),
            typeof(ICapacidadOperativaService),
            typeof(IKpisService),
            typeof(IMarcacionTurnoService),
            typeof(IPrediccionAfluenciaService),
            typeof(IGeneracionPrediccionService),
            typeof(IEntrenamientoModeloService),
            typeof(IRecomendacionesService),
            typeof(IRegistroAfluenciaRepository),
            typeof(ICapacidadOperativaRepository),
            typeof(IKpisRepository),
            typeof(IMarcacionTurnoRepository),
            typeof(IPrediccionAfluenciaRepository),
            typeof(IGeneracionPrediccionRepository),
            typeof(IModelosAfluenciaRepository),
            typeof(IModelosActivosRepository),
            typeof(IRecomendacionesRepository)
        };

        foreach (var tipo in tipos)
        {
            Assert.Equal(1, servicios.Count(descriptor => descriptor.ServiceType == tipo));
            Assert.Single(scope.ServiceProvider.GetServices(tipo));
        }

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IModelosAfluenciaRepository>(),
            scope.ServiceProvider.GetRequiredService<IModelosActivosRepository>());

        var procesos = proveedor.GetServices<IHostedService>().ToList();
        Assert.Single(procesos.OfType<GeneracionPrediccionBackgroundService>());
        Assert.Single(procesos.OfType<CapacidadOperativaBackgroundService>());
        Assert.Single(procesos.OfType<EntrenamientoModeloBackgroundService>());
        Assert.Single(procesos.OfType<RecomendacionesBackgroundService>());
    }

    [Fact]
    public void RechazaUmbralesDeRecomendacionInvalidosDuranteElRegistro()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GiraDb"] = "Server=(localdb)\\MSSQLLocalDB;Database=GiraDiTest;Trusted_Connection=True;TrustServerCertificate=True",
            ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
            ["Jwt:Issuer"] = "GIRA.Api",
            ["Jwt:Audience"] = "GIRA.Frontend",
            ["Analitica:Recomendaciones:PorcentajeNoShow"] = "101"
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuracion));

        Assert.Contains("umbral", error.Message);
    }
}
