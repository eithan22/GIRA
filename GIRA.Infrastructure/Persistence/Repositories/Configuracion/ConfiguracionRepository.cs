using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Exceptions.Configuracion;
using GIRA.Infrastructure.Persistence.Configurations.Configuracion;
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

    /// <inheritdoc />
    public Task<ConfiguracionRestaurante?> BuscarConfiguracionAsync(CancellationToken cancellationToken) =>
        _contexto.ConfiguracionesRestaurante.FirstOrDefaultAsync(c => c.Id == ConfiguracionRestaurante.IdUnico, cancellationToken);

    /// <inheritdoc />
    public void AgregarConfiguracion(ConfiguracionRestaurante configuracion) =>
        _contexto.ConfiguracionesRestaurante.Add(configuracion);

    /// <inheritdoc />
    public Task<HorarioOperacion?> BuscarHorarioAsync(DayOfWeek dia, CancellationToken cancellationToken) =>
        _contexto.HorariosOperacion.FirstOrDefaultAsync(h => h.DiaSemana == dia, cancellationToken);

    /// <inheritdoc />
    public void AgregarHorario(HorarioOperacion horario) => _contexto.HorariosOperacion.Add(horario);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiaCerrado>> ListarDiasCerradosDesdeAsync(DateOnly desde, CancellationToken cancellationToken) =>
        await _contexto.DiasCerrados.AsNoTracking()
            .Where(d => d.Fecha >= desde)
            .OrderBy(d => d.Fecha)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteDiaCerradoAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        _contexto.DiasCerrados.AnyAsync(d => d.Fecha == fecha, cancellationToken);

    /// <inheritdoc />
    public Task<DiaCerrado?> BuscarDiaCerradoAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.DiasCerrados.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc />
    public void AgregarDiaCerrado(DiaCerrado diaCerrado) => _contexto.DiasCerrados.Add(diaCerrado);

    /// <inheritdoc />
    public void EliminarDiaCerrado(DiaCerrado diaCerrado) => _contexto.DiasCerrados.Remove(diaCerrado);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConfiguracionConflictoConcurrenciaException("La configuración cambió mientras se guardaba.", ex);
        }
        catch (DbUpdateException ex) when (
            ViolacionesIndiceUnico.Es(ex, DiaCerradoConfiguration.IndiceFecha)
            || ViolacionesIndiceUnico.Es(ex, HorarioOperacionConfiguration.IndiceDia)
            || ViolacionesIndiceUnico.Es(ex, "PK_ConfiguracionRestaurante"))
        {
            throw new ConfiguracionDuplicadaException("Otra solicitud acaba de crear ese registro.", ex);
        }
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
