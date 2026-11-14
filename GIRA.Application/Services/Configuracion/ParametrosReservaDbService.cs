using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Reservas;
using Microsoft.Extensions.Caching.Memory;

namespace GIRA.Application.Services.Configuracion;

/// <summary>
/// Implementación de <see cref="IParametrosReservaService"/> que lee la configuración de la base de datos
/// y guarda la instantánea en <see cref="IMemoryCache"/> durante <see cref="DuracionCache"/>. Lo que falte
/// (sin fila de configuración o sin horario de algún día) se completa con los valores por defecto, así que
/// la API funciona aunque el seeder no se haya ejecutado.
/// </summary>
/// <remarks>
/// Se registra Scoped (usa un repositorio con contexto de EF); la caché es Singleton y se comparte. Con
/// varias instancias de la API, cada una tiene su propia caché: un cambio tarda como máximo
/// <see cref="DuracionCache"/> en verse en las demás.
/// </remarks>
public class ParametrosReservaDbService : IParametrosReservaService
{
    /// <summary>Clave de la instantánea en la caché.</summary>
    public const string ClaveCache = "configuracion:parametros-reserva";

    /// <summary>Tiempo máximo que se conserva una instantánea.</summary>
    public static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(2);

    private readonly IConfiguracionRepository _repositorio;
    private readonly IMemoryCache _cache;

    /// <summary>Crea el servicio con el repositorio de configuración y la caché.</summary>
    public ParametrosReservaDbService(IConfiguracionRepository repositorio, IMemoryCache cache)
    {
        _repositorio = repositorio;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<ParametrosReserva> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ClaveCache, out ParametrosReserva? enCache) && enCache is not null)
        {
            return enCache;
        }

        var instantanea = Construir(await _repositorio.LeerAsync(cancellationToken));
        _cache.Set(ClaveCache, instantanea, DuracionCache);
        return instantanea;
    }

    /// <inheritdoc />
    public void Invalidar() => _cache.Remove(ClaveCache);

    /// <summary>Arma la instantánea a partir de lo guardado, completando lo que falte con los valores por defecto.</summary>
    public static ParametrosReserva Construir(ConfiguracionGuardada guardada)
    {
        var porDefecto = ParametrosReservaService.PorDefecto();

        var horarios = new Dictionary<DayOfWeek, HorarioDia>(porDefecto.Horarios);
        foreach (var horario in guardada.Horarios)
        {
            horarios[horario.DiaSemana] = new HorarioDia(horario.Abierto, horario.HoraApertura, horario.HoraCierre);
        }

        var diasCerrados = guardada.DiasCerrados.Select(d => d.Fecha).ToHashSet();

        if (guardada.Configuracion is not { } c)
        {
            return porDefecto with { Horarios = horarios, DiasCerrados = diasCerrados };
        }

        return new ParametrosReserva(
            TimeSpan.FromMinutes(c.AntelacionMinimaMinutos),
            TimeSpan.FromDays(c.AntelacionMaximaDias),
            TimeSpan.FromMinutes(c.LimiteCancelacionMinutos),
            TimeSpan.FromMinutes(c.VentanaCheckInMinutos),
            c.MaxReservasVigentesPorTelefono,
            c.MaxPersonasPorReserva,
            horarios,
            diasCerrados);
    }
}
