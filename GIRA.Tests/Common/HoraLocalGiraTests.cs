using GIRA.Application.Common;

namespace GIRA.Tests.Common;

/// <summary>
/// Confirma que la zona horaria de Santo Domingo se resuelve y que la conversión
/// funciona correctamente cerca de medianoche UTC (donde más fácil es equivocarse).
/// </summary>
public class HoraLocalGiraTests
{
    [Fact]
    public void ZonaHoraria_SeResuelveConDesplazamientoDeCuatroHoras()
    {
        Assert.NotNull(HoraLocalGira.ZonaHoraria);
        Assert.Equal(TimeSpan.FromHours(-4), HoraLocalGira.ZonaHoraria.BaseUtcOffset);
    }

    [Fact]
    public void AhoraLocal_02hUtcDelDomingo_Son22hDelSabadoEnSantoDomingo()
    {
        var utc = new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);
        var reloj = new TimeProviderFalso(utc);

        var ahoraLocal = reloj.AhoraLocal();

        Assert.Equal(new DateTime(2026, 11, 7, 22, 0, 0), ahoraLocal);
    }

    [Fact]
    public void HoyLocal_02hUtcDelDomingo_ElDiaLocalEsElSabado()
    {
        var utc = new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);
        var reloj = new TimeProviderFalso(utc);

        var hoyLocal = reloj.HoyLocal();

        Assert.Equal(new DateOnly(2026, 11, 7), hoyLocal);
    }
}
