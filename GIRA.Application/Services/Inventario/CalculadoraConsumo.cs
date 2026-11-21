using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Entities.Inventario;

namespace GIRA.Application.Services.Inventario;

/// <summary>Comensales de un día. Se usan los más altos entre reservas y predicción (SRS 5.3).</summary>
public sealed record ComensalesDia(DateOnly Fecha, int ConReserva, int? Prediccion)
{
    /// <summary>Comensales que entran en el cálculo.</summary>
    public int Usados => Math.Max(ConReserva, Prediccion ?? 0);
}

/// <summary>Lo necesario para proyectar: consumo por comensal de cada insumo y comensales de cada día.</summary>
public sealed class BaseProyeccion
{
    /// <summary>Crea la base con los factores y los días.</summary>
    public BaseProyeccion(IReadOnlyDictionary<Guid, decimal> factorPorInsumo, IReadOnlyList<ComensalesDia> dias)
    {
        FactorPorInsumo = factorPorInsumo;
        Dias = dias;
    }

    /// <summary>Consumo de un comensal, por insumo. Los insumos que no están en recetas activas no aparecen.</summary>
    public IReadOnlyDictionary<Guid, decimal> FactorPorInsumo { get; }

    /// <summary>Días proyectados, en orden.</summary>
    public IReadOnlyList<ComensalesDia> Dias { get; }

    /// <summary>Consumo esperado del insumo en ese día, redondeado a 3 decimales.</summary>
    public decimal Consumo(Guid insumoId, ComensalesDia dia) =>
        Math.Round(FactorPorInsumo.GetValueOrDefault(insumoId) * dia.Usados, 3);

    /// <summary>Lo que quedaría al final de cada día, empezando con <paramref name="stock"/>.</summary>
    public IReadOnlyList<decimal> Restantes(Guid insumoId, decimal stock)
    {
        var restantes = new List<decimal>(Dias.Count);
        foreach (var dia in Dias)
        {
            stock -= Consumo(insumoId, dia);
            restantes.Add(stock);
        }

        return restantes;
    }
}

/// <summary>
/// Fórmula del SRS 5.3: Consumo(i, f) = Comensales(f) × Σ PorcentajeDemanda(p) × Cantidad(i, p) ÷ Porciones(p).
/// </summary>
public class CalculadoraConsumo
{
    private readonly IPlatoRepository _platos;
    private readonly IConsultaReservasInventario _reservas;
    private readonly IConsumoProyectadoRepository _consumos;

    /// <summary>Crea la calculadora con los platos, el puerto hacia Reservas y las predicciones guardadas.</summary>
    public CalculadoraConsumo(IPlatoRepository platos, IConsultaReservasInventario reservas, IConsumoProyectadoRepository consumos)
    {
        _platos = platos;
        _reservas = reservas;
        _consumos = consumos;
    }

    /// <summary>
    /// Consumo de un comensal por insumo: Σ PorcentajeDemanda/100 × Cantidad ÷ Porciones. Solo cuentan los
    /// platos activos con receta de al menos un insumo (RN-INV-04).
    /// </summary>
    public static IReadOnlyDictionary<Guid, decimal> Factores(IEnumerable<Plato> platos)
    {
        var factores = new Dictionary<Guid, decimal>();
        foreach (var plato in platos.Where(p => p.Activo && p.Receta is { Porciones: > 0 } && p.Receta.Detalles.Count > 0))
        {
            foreach (var linea in plato.Receta!.Detalles)
            {
                factores[linea.InsumoId] = factores.GetValueOrDefault(linea.InsumoId)
                    + plato.PorcentajeDemanda / 100m * linea.Cantidad / plato.Receta.Porciones;
            }
        }

        return factores;
    }

    /// <summary>Factores de las recetas actuales y comensales de cada día entre <paramref name="desde"/> y <paramref name="hasta"/>.</summary>
    public async Task<BaseProyeccion> CalcularAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var platos = await _platos.ListarActivosConRecetaAsync(cancellationToken);
        var personas = await _reservas.PersonasConReservaPorFechaAsync(desde, hasta, cancellationToken);
        // Sin predicción guardada (Analítica todavía no la publicó) el día usa solo las reservas.
        var predicciones = (await _consumos.ListarPrediccionesAsync(desde, hasta, cancellationToken))
            .ToDictionary(p => p.Fecha, p => p.Comensales);

        var dias = Enumerable.Range(0, hasta.DayNumber - desde.DayNumber + 1)
            .Select(desde.AddDays)
            .Select(fecha => new ComensalesDia(
                fecha, personas.GetValueOrDefault(fecha), predicciones.TryGetValue(fecha, out var p) ? p : null))
            .ToList();

        return new BaseProyeccion(Factores(platos), dias);
    }
}
