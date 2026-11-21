using GIRA.Application.DTOs.Reservas;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas de los cálculos propios de <see cref="ParametrosReserva"/> (sin servicios de por medio).</summary>
public class ParametrosReservaTests
{
    private static ParametrosReserva Crear(TimeSpan antelacionMaxima) => new(
        AntelacionMinima: TimeSpan.Zero,
        AntelacionMaxima: antelacionMaxima,
        LimiteCancelacion: TimeSpan.Zero,
        VentanaCheckIn: TimeSpan.Zero,
        ToleranciaNoShow: TimeSpan.Zero,
        MaxReservasVigentesPorTelefono: 1,
        MaxPersonasPorReserva: 1,
        TelefonoContacto: null,
        Horarios: new Dictionary<DayOfWeek, HorarioDia>(),
        DiasCerrados: new HashSet<DateOnly>());

    [Fact]
    public void DiasAntelacionMaxima_ConfiguracionActualDeDiasEnteros_NoCambia()
    {
        var parametros = Crear(TimeSpan.FromDays(30));

        Assert.Equal(30, parametros.DiasAntelacionMaxima);
        Assert.Equal(new DateOnly(2026, 1, 31), parametros.FechaMaximaReservable(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void DiasAntelacionMaxima_ConUnaFraccionDeDia_RedondeaHaciaArribaSinTruncar()
    {
        // Hoy la configuración siempre guarda días enteros; esta prueba demuestra que, si algún día
        // dejara de serlo, el cálculo no trunca (30.5 días no se convierte en 30, como pasaba con
        // TimeSpan.Days antes de GI-afluencia).
        var parametros = Crear(TimeSpan.FromDays(30.5));

        Assert.Equal(31, parametros.DiasAntelacionMaxima);
        Assert.Equal(new DateOnly(2026, 2, 1), parametros.FechaMaximaReservable(new DateOnly(2026, 1, 1)));
    }
}
