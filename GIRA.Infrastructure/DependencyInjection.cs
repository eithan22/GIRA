using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Interceptors;
using GIRA.Infrastructure.Persistence.Repositories.Configuracion;
using GIRA.Infrastructure.Persistence.Repositories.Personal;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using GIRA.Infrastructure.Persistence.Repositories.Inventario;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Persistence.Repositories.Personal;
using GIRA.Infrastructure.Services.Inventario;
using GIRA.Infrastructure.Services.Analitica;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Infrastructure.Services.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Services.Analitica;


namespace GIRA.Infrastructure;

/// <summary>
/// Punto único de registro de los servicios de GIRA.Infrastructure
/// (persistencia y ASP.NET Core Identity) en el contenedor de dependencias de la API.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra <see cref="GiraDbContext"/> contra SQL Server, configura ASP.NET Core
    /// Identity con las políticas de contraseña y bloqueo del proyecto y registra los
    /// servicios de autenticación.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GiraDb")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'GiraDb'. Configúrala en User Secrets con la clave 'ConnectionStrings:GiraDb'.");

        services.AddHttpContextAccessor();
        services.AddScoped<AuditoriaSaveChangesInterceptor>();

        services.AddDbContext<GiraDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>()));

        services.AddSeguridad(configuration);
        // Caché en memoria que usa ParametrosReservaDbService (Application solo conoce IMemoryCache).
        services.AddMemoryCache();
        services.AddConfiguracionRepositories();
        services.AddReservasRepositories();
        services.AddPersonalRepositories();
        services.AddReservasServicios(configuration);
        services.AddInventarioRepositories();
        services.AddInventarioServicios(configuration);
        services.AddScoped<IRegistroAfluenciaRepository, RegistroAfluenciaRepository>();
        services.AddScoped<ICapacidadOperativaRepository, CapacidadOperativaRepository>();
        services.AddScoped<IMarcacionTurnoRepository, MarcacionTurnoRepository>();
        services.AddScoped<IKpisRepository, KpisRepository>();
        services.AddScoped<IPrediccionAfluenciaRepository, PrediccionAfluenciaRepository>();
        services.AddScoped<IGeneracionPrediccionRepository>(sp =>
            new GeneracionPrediccionRepository(sp.GetRequiredService<GiraDbContext>(), configuration));
        services.AddScoped<IModelosAfluenciaRepository, ModelosAfluenciaRepository>();
        services.AddScoped<IModelosActivosRepository>(sp =>
            sp.GetRequiredService<IModelosAfluenciaRepository>());
        services.AddScoped<IModeloSsaService, ModeloSsaService>();
        services.AddScoped<IRecomendacionesRepository, RecomendacionesRepository>();
        var opcionesRecomendaciones = new OpcionesRecomendaciones();
        configuration.GetSection("Analitica:Recomendaciones").Bind(opcionesRecomendaciones);
        if (opcionesRecomendaciones.PorcentajeAumentoDemanda < 0 ||
            opcionesRecomendaciones.PorcentajeNoShow is < 0 or > 100 || opcionesRecomendaciones.AlertasMensuales < 0)
            throw new InvalidOperationException("Los umbrales de recomendaciones no son válidos.");
        services.AddSingleton(opcionesRecomendaciones);
        if (configuration.GetValue<bool?>("Analitica:Recomendaciones:Habilitada") != false)
            services.AddHostedService<RecomendacionesBackgroundService>();
        if (configuration.GetValue<bool>("Analitica:Generacion:Habilitada"))
        {
            _ = GeneracionPrediccionRepository.ObtenerRestauranteId(configuration);
            services.AddHostedService<GeneracionPrediccionBackgroundService>();
        }
        if (configuration.GetValue<bool?>("Analitica:Capacidad:Habilitada") != false)
            services.AddHostedService<CapacidadOperativaBackgroundService>();
        if (configuration.GetValue<bool?>("Analitica:Entrenamiento:Habilitada") != false)
            services.AddHostedService<EntrenamientoModeloBackgroundService>();

        return services;
    }
}
