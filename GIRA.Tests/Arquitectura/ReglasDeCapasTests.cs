using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using System.Reflection;

namespace GIRA.Tests.Arquitectura;

/// <summary>
/// Pruebas de arquitectura (Tanda F, 4.9): verifican por reflexión pura que las capas de
/// Clean Architecture (Domain -> Application -> Infrastructure -> Api) no se violen.
/// No abren conexión a base de datos ni dependen de Docker; deben correr siempre con
/// Category!=Integration.
/// Limitación conocida: <see cref="Assembly.GetReferencedAssemblies"/> solo lista las
/// referencias que el compilador conservó en el ensamblado resultante; una referencia de
/// proyecto de la que no se usa ningún tipo puede no aparecer en la lista.
/// </summary>
public class ReglasDeCapasTests
{
    private static readonly Assembly EnsambladoDomain = typeof(GIRA.Domain.Constants.Politicas).Assembly;
    private static readonly Assembly EnsambladoApplication = typeof(GIRA.Application.DependencyInjection).Assembly;
    private static readonly Assembly EnsambladoInfrastructure = typeof(GIRA.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly EnsambladoApi = typeof(Program).Assembly;

    private static bool Referencia(Assembly origen, Assembly destino) =>
        origen.GetReferencedAssemblies()
            .Any(referencia => referencia.Name == destino.GetName().Name);

    /// <summary>GIRA.Domain no debe referenciar Application, Infrastructure ni Api.</summary>
    [Fact]
    public void Domain_ReferenciasDeEnsamblado_NoIncluyeCapasSuperiores()
    {
        Assert.False(
            Referencia(EnsambladoDomain, EnsambladoApplication),
            "GIRA.Domain no debe referenciar a GIRA.Application.");

        Assert.False(
            Referencia(EnsambladoDomain, EnsambladoInfrastructure),
            "GIRA.Domain no debe referenciar a GIRA.Infrastructure.");

        Assert.False(
            Referencia(EnsambladoDomain, EnsambladoApi),
            "GIRA.Domain no debe referenciar a GIRA.Api.");
    }

    /// <summary>
    /// GIRA.Application no debe referenciar Infrastructure ni Api. No se exige la positiva
    /// "referencia a Domain": el ProjectReference a GIRA.Domain está declarado en el .csproj
    /// pero hoy ningún tipo de Application lo usa, así que el compilador lo descarta y no
    /// aparece en GetReferencedAssemblies. Se activará cuando se agreguen entidades de dominio
    /// de los demás módulos. La prueba anti-vacío del mecanismo vive en la regla de Infrastructure.
    /// </summary>
    [Fact]
    public void Application_ReferenciasDeEnsamblado_NoIncluyeInfrastructureNiApi()
    {
        Assert.False(
            Referencia(EnsambladoApplication, EnsambladoInfrastructure),
            "GIRA.Application no debe referenciar a GIRA.Infrastructure.");

        Assert.False(
            Referencia(EnsambladoApplication, EnsambladoApi),
            "GIRA.Application no debe referenciar a GIRA.Api.");
    }

    /// <summary>GIRA.Infrastructure no debe referenciar Api.</summary>
    [Fact]
    public void Infrastructure_ReferenciasDeEnsamblado_NoIncluyeApi()
    {
        // Anti-vacío: confirma con referencias reales que la comprobación de "sí referencia" funciona.
        Assert.True(
            Referencia(EnsambladoInfrastructure, EnsambladoDomain),
            "GIRA.Infrastructure debería referenciar a GIRA.Domain.");

        Assert.True(
            Referencia(EnsambladoInfrastructure, EnsambladoApplication),
            "GIRA.Infrastructure debería referenciar a GIRA.Application.");

        Assert.False(
            Referencia(EnsambladoInfrastructure, EnsambladoApi),
            "GIRA.Infrastructure no debe referenciar a GIRA.Api.");
    }

    /// <summary>
    /// Ningún controller debe depender de GiraDbContext, UserManager, SignInManager ni
    /// RoleManager directamente: debe usar los servicios de Application/Infrastructure.
    /// </summary>
    [Fact]
    public void ControllersDeApi_MiembrosYConstructores_NoUsanDbContextNiIdentityManagers()
    {
        var tiposProhibidos = new HashSet<Type>
        {
            typeof(GiraDbContext),
            typeof(UserManager<>),
            typeof(SignInManager<>),
            typeof(RoleManager<>)
        };

        var controllers = EnsambladoApi.GetTypes()
            .Where(tipo => tipo.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToList();

        // Anti-vacío: si no hubiera controllers que revisar, la regla no estaría probando nada
        // (hoy hay 5: Auth, Me, Roles, Usuarios, UsuarioAccesos).
        Assert.True(
            controllers.Count >= 1,
            "No se encontró ninguna clase cuyo nombre termine en 'Controller' en GIRA.Api; " +
            "la regla no puede verificar nada sin al menos un controller.");

        var violaciones = new List<string>();

        foreach (var controller in controllers)
        {
            var miembros =
                controller.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(campo => (campo.Name, Tipo: campo.FieldType))
                .Concat(controller.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(propiedad => (propiedad.Name, Tipo: propiedad.PropertyType)))
                .Concat(controller.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parametro => (parametro.Name ?? string.Empty, Tipo: parametro.ParameterType)));

            foreach (var (nombre, tipo) in miembros)
            {
                var tipoAComparar = tipo.IsGenericType ? tipo.GetGenericTypeDefinition() : tipo;

                if (tiposProhibidos.Contains(tipoAComparar))
                {
                    violaciones.Add($"{controller.Name}.{nombre} ({tipo.Name})");
                }
            }
        }

        Assert.True(
            violaciones.Count == 0,
            "Los siguientes controllers de GIRA.Api dependen directamente de " +
            "GiraDbContext/UserManager/SignInManager/RoleManager en vez de usar un servicio " +
            $"de Application/Infrastructure: {string.Join(", ", violaciones)}");
    }
}
