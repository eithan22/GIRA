using System.Globalization;

namespace GIRA.Application.Common.Inventario;

/// <summary>Parámetros y reglas pequeñas de Inventario que usan varios servicios.</summary>
public static class ReglasInventario
{
    /// <summary>Días máximos de la proyección, contando hoy.</summary>
    public const int DiasMaximosProyeccion = 14;

    /// <summary>La suma de demanda de los platos activos debe quedar entre estos valores (RN-INV-05).</summary>
    public const decimal DemandaMinima = 95m, DemandaMaxima = 105m;

    /// <summary>Niveles de stock que muestra el catálogo.</summary>
    public const string NivelNormal = "Normal", NivelBajo = "Bajo", NivelCritico = "Critico";

    /// <summary>
    /// Nivel del stock. Propuesta pendiente de confirmar con el equipo (GI-6): Crítico en la mitad del
    /// punto de reorden o menos, Bajo en el punto de reorden o menos, Normal por encima.
    /// </summary>
    public static string Nivel(decimal stock, decimal puntoReorden) =>
        stock <= puntoReorden / 2 ? NivelCritico : stock <= puntoReorden ? NivelBajo : NivelNormal;

    /// <summary>Aviso de RN-INV-05 si la suma de demanda sale del rango; null si está dentro o no hay platos activos.</summary>
    public static string? AdvertenciaDemanda(decimal suma, bool hayActivos) =>
        !hayActivos || (suma >= DemandaMinima && suma <= DemandaMaxima)
            ? null
            : $"La suma del porcentaje de demanda de los platos activos es {suma:0.##} %. Debe quedar entre 95 % y 105 % para que la proyección sea confiable.";

    /// <summary>True si <paramref name="valor"/> es el nombre de uno de los <paramref name="permitidos"/> (sin importar mayúsculas).</summary>
    public static bool EsUno<T>(string? valor, params T[] permitidos) where T : struct, Enum =>
        valor is not null && permitidos.Any(p => string.Equals(p.ToString(), valor.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Convierte el texto ya validado con <see cref="EsUno{T}"/> en el enum.</summary>
    public static T Leer<T>(string valor) where T : struct, Enum => Enum.Parse<T>(valor.Trim(), ignoreCase: true);

    /// <summary>Cantidad para mensajes: hasta 3 decimales con punto, sin depender de la cultura del servidor.</summary>
    public static string Texto(decimal cantidad) => cantidad.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Fecha para mensajes en dd/mm/aaaa (RNF-16), sin depender de la cultura del servidor.</summary>
    public static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Texto recortado, o null si queda vacío.</summary>
    public static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Las fechas de la base llegan sin zona; se marcan como UTC para que el JSON lleve la "Z".</summary>
    public static DateTime Utc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

    /// <summary>Comienzo del día local <paramref name="fecha"/> (America/Santo_Domingo), en UTC.</summary>
    public static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria);
}
