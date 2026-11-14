using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Persistence.Repositories.Configuracion;

/// <summary>Implementación de <see cref="IConfiguracionRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class ConfiguracionRepository : IConfiguracionRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public ConfiguracionRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<ConfiguracionGuardada> LeerAsync(CancellationToken cancellationToken)
    {
        var configuracion = await _contexto.ConfiguracionesRestaurante
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == ConfiguracionRestaurante.IdUnico, cancellationToken);
        var horarios = await _contexto.HorariosOperacion.AsNoTracking().ToListAsync(cancellationToken);
        var diasCerrados = await _contexto.DiasCerrados.AsNoTracking().ToListAsync(cancellationToken);

        return new ConfiguracionGuardada(configuracion, horarios, diasCerrados);
    }
}

/// <summary>Registra los repositorios del módulo Configuración.</summary>
public static class ConfiguracionRepositoriesExtensions
{
    /// <summary>Registra <see cref="IConfiguracionRepository"/>.</summary>
    public static IServiceCollection AddConfiguracionRepositories(this IServiceCollection services)
    {
        services.AddScoped<IConfiguracionRepository, ConfiguracionRepository>();
        return services;
    }
}
