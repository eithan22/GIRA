using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Exceptions.Personal;
using GIRA.Infrastructure.Persistence.Configurations.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="ICargoRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class CargoRepository : ICargoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public CargoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) =>
        await _contexto.Cargos
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Cargos.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var consulta = _contexto.Cargos.AsNoTracking().Where(c => c.Nombre == nombre);

        if (excluirId.HasValue)
        {
            consulta = consulta.Where(c => c.Id != excluirId.Value);
        }

        return await consulta.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Agregar(Cargo cargo) => _contexto.Cargos.Add(cargo);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, CargoConfiguration.IndiceNombre))
        {
            throw new CargoNombreDuplicadoException("Otra solicitud acaba de registrar ese nombre de cargo.", ex);
        }
    }
}

/// <summary>Registra los repositorios del módulo Personal y Turnos.</summary>
public static class PersonalRepositoriesExtensions
{
    /// <summary>Registra <see cref="ICargoRepository"/>.</summary>
    public static IServiceCollection AddPersonalRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICargoRepository, CargoRepository>();
        services.AddScoped<IEmpleadoRepository, EmpleadoRepository>();
        services.AddScoped<IDisponibilidadRepository, DisponibilidadRepository>();
        services.AddScoped<ITurnoRepository, TurnoRepository>();
        services.AddScoped<IAusenciaRepository, AusenciaRepository>();
        services.AddScoped<IRequerimientoPersonalRepository, RequerimientoPersonalRepository>();
        services.AddScoped<ISugerenciaTurnoRepository, SugerenciaTurnoRepository>();
        return services;
    }
}
