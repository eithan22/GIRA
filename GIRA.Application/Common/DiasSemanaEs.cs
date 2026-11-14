namespace GIRA.Application.Common;

/// <summary>Nombres en español de los días de la semana y formato de horas para los mensajes.</summary>
public static class DiasSemanaEs
{
    private static readonly string[] Nombres =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    /// <summary>Nombre del día en minúsculas ("lunes").</summary>
    public static string Nombre(DayOfWeek dia) => Nombres[(int)dia];

    /// <summary>Días en el orden en que se muestran: de lunes a domingo.</summary>
    public static IReadOnlyList<DayOfWeek> LunesADomingo { get; } =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    /// <summary>Hora como "HH:mm".</summary>
    public static string Hora(TimeOnly hora) => hora.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
