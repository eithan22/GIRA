using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace GIRA.Infrastructure.Services.Reservas;

/// <summary>
/// Configuración del proceso automático de reservas (sección <c>Reservas:ProcesoAutomatico</c>).
/// Leerla nunca lanza: un valor ausente usa el valor por defecto y uno inválido también, pero deja una
/// advertencia que el servicio registra al arrancar.
/// </summary>
public sealed class ProcesoAutomaticoReservasOptions
{
    /// <summary>Sección de configuración.</summary>
    public const string Seccion = "Reservas:ProcesoAutomatico";

    /// <summary>Intervalo por defecto entre ciclos: un minuto.</summary>
    public const int IntervaloPorDefecto = 60;

    /// <summary>Intervalo mínimo permitido, en segundos.</summary>
    public const int IntervaloMinimo = 5;

    /// <summary>Intervalo máximo permitido, en segundos.</summary>
    public const int IntervaloMaximo = 3600;

    /// <summary>Retraso por defecto del primer ciclo, para no competir con <c>Migrate()</c> y el seeder del arranque.</summary>
    public const int RetrasoInicialPorDefecto = 15;

    /// <summary>Retraso inicial máximo permitido, en segundos.</summary>
    public const int RetrasoInicialMaximo = 600;

    /// <summary>Si es false, el proceso no se registra (se usa en las pruebas HTTP).</summary>
    public bool Habilitado { get; init; } = true;

    /// <summary>Segundos entre ciclos (5 a 3600).</summary>
    public int IntervaloSegundos { get; init; } = IntervaloPorDefecto;

    /// <summary>Segundos que espera el primer ciclo tras el arranque (0 a 600).</summary>
    public int RetrasoInicialSegundos { get; init; } = RetrasoInicialPorDefecto;

    /// <summary>Avisos por valores de configuración inválidos; el servicio los registra como Warning.</summary>
    public IReadOnlyList<string> Advertencias { get; init; } = [];

    /// <summary>Lee la configuración sin lanzar nunca: lo inválido cae al valor por defecto con una advertencia.</summary>
    public static ProcesoAutomaticoReservasOptions Desde(IConfiguration configuracion)
    {
        var advertencias = new List<string>();

        var habilitado = true;
        var textoHabilitado = configuracion[$"{Seccion}:Habilitado"];
        if (textoHabilitado is not null && !bool.TryParse(textoHabilitado, out habilitado))
        {
            habilitado = true;
            advertencias.Add($"El valor '{textoHabilitado}' de {Seccion}:Habilitado no es true ni false; se deja habilitado.");
        }

        var intervalo = LeerEntero(
            configuracion, "IntervaloSegundos", IntervaloPorDefecto, IntervaloMinimo, IntervaloMaximo, advertencias);
        var retraso = LeerEntero(
            configuracion, "RetrasoInicialSegundos", RetrasoInicialPorDefecto, 0, RetrasoInicialMaximo, advertencias);

        return new ProcesoAutomaticoReservasOptions
        {
            Habilitado = habilitado,
            IntervaloSegundos = intervalo,
            RetrasoInicialSegundos = retraso,
            Advertencias = advertencias
        };
    }

    private static int LeerEntero(
        IConfiguration configuracion, string clave, int porDefecto, int minimo, int maximo, List<string> advertencias)
    {
        var texto = configuracion[$"{Seccion}:{clave}"];
        if (texto is null)
        {
            return porDefecto;
        }

        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            && valor >= minimo && valor <= maximo)
        {
            return valor;
        }

        advertencias.Add(
            $"El valor '{texto}' de {Seccion}:{clave} no es válido (debe ser un entero entre {minimo} y {maximo}); se usa {porDefecto}.");
        return porDefecto;
    }
}
