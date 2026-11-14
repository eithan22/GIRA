using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Validations.Configuracion;
using GIRA.Tests.Common;

namespace GIRA.Tests.Configuracion;

/// <summary>Validadores de la configuración de reservas (GI-226): rangos y relaciones, con mensajes en español.</summary>
public class ConfiguracionReservasValidatorsTests
{
    private static readonly ActualizarParametrosRequestValidator Parametros = new();
    private static readonly ActualizarHorarioRequestValidator Horario = new();

    private static ActualizarParametrosRequest Valida() => new(
        AntelacionMinimaMinutos: 120, AntelacionMaximaDias: 30, LimiteCancelacionMinutos: 120,
        VentanaCheckInMinutos: 15, MaxReservasVigentesPorTelefono: 2, MaxPersonasPorReserva: 8);

    private static async Task<Dictionary<string, string[]>> ErroresAsync(ActualizarParametrosRequest request)
    {
        var resultado = await Parametros.ValidateAsync(request);
        return resultado.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }

    [Fact]
    public async Task Parametros_ConLosValoresPorDefecto_SonValidos()
    {
        Assert.Empty(await ErroresAsync(Valida()));
    }

    [Fact]
    public async Task AntelacionMinima_NoPuedeSerNegativa()
    {
        var errores = await ErroresAsync(Valida() with { AntelacionMinimaMinutos = -1 });

        Assert.Equal("La antelación mínima no puede ser negativa.", Assert.Single(errores["AntelacionMinimaMinutos"]));
    }

    [Fact]
    public async Task AntelacionMinima_DebeSerMenorQueLaMaxima()
    {
        // Máxima de 1 día = 1440 minutos: 1440 no es menor; 1439 sí.
        var igual = await ErroresAsync(Valida() with { AntelacionMaximaDias = 1, AntelacionMinimaMinutos = 1440, LimiteCancelacionMinutos = 0 });
        var menor = await ErroresAsync(Valida() with { AntelacionMaximaDias = 1, AntelacionMinimaMinutos = 1439, LimiteCancelacionMinutos = 0 });

        Assert.Equal("La antelación mínima debe ser menor que la antelación máxima.", Assert.Single(igual["AntelacionMinimaMinutos"]));
        Assert.Empty(menor);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public async Task AntelacionMaxima_EntreUnoY365Dias(int dias, bool valida)
    {
        var errores = await ErroresAsync(Valida() with { AntelacionMaximaDias = dias });

        Assert.Equal(valida, !errores.ContainsKey("AntelacionMaximaDias"));
        if (!valida)
        {
            Assert.Equal("La antelación máxima debe estar entre 1 y 365 días.", Assert.Single(errores["AntelacionMaximaDias"]));
        }
    }

    [Fact]
    public async Task LimiteCancelacion_NoPuedeSerNegativoNiSuperarLaAntelacionMaxima()
    {
        var negativo = await ErroresAsync(Valida() with { LimiteCancelacionMinutos = -5 });
        var excede = await ErroresAsync(Valida() with { AntelacionMaximaDias = 1, AntelacionMinimaMinutos = 0, LimiteCancelacionMinutos = 1441 });
        var enElLimite = await ErroresAsync(Valida() with { AntelacionMaximaDias = 1, AntelacionMinimaMinutos = 0, LimiteCancelacionMinutos = 1440 });

        Assert.Equal("El límite de cancelación no puede ser negativo.", Assert.Single(negativo["LimiteCancelacionMinutos"]));
        Assert.Equal("El límite de cancelación no puede superar la antelación máxima.", Assert.Single(excede["LimiteCancelacionMinutos"]));
        Assert.Empty(enElLimite);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public async Task VentanaCheckIn_EntreCeroY120Minutos(int minutos, bool valida)
    {
        var errores = await ErroresAsync(Valida() with { VentanaCheckInMinutos = minutos });

        Assert.Equal(valida, !errores.ContainsKey("VentanaCheckInMinutos"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task MaxReservasVigentesPorTelefono_EntreUnoYDiez(int maximo, bool valida)
    {
        var errores = await ErroresAsync(Valida() with { MaxReservasVigentesPorTelefono = maximo });

        Assert.Equal(valida, !errores.ContainsKey("MaxReservasVigentesPorTelefono"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task MaxPersonasPorReserva_EntreUnoYCincuenta(int maximo, bool valida)
    {
        var errores = await ErroresAsync(Valida() with { MaxPersonasPorReserva = maximo });

        Assert.Equal(valida, !errores.ContainsKey("MaxPersonasPorReserva"));
    }

    [Fact]
    public async Task Horario_DiaAbierto_LaAperturaDebeSerAnteriorAlCierre()
    {
        var invalido = await Horario.ValidateAsync(new ActualizarHorarioRequest(true, new TimeOnly(22, 0), new TimeOnly(12, 0)));
        var igual = await Horario.ValidateAsync(new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(12, 0)));
        var valido = await Horario.ValidateAsync(new ActualizarHorarioRequest(true, new TimeOnly(12, 0), new TimeOnly(22, 30)));

        Assert.Equal("La hora de apertura debe ser anterior a la hora de cierre.", Assert.Single(invalido.Errors).ErrorMessage);
        Assert.Equal("HoraApertura", Assert.Single(invalido.Errors).PropertyName);
        Assert.False(igual.IsValid);
        Assert.True(valido.IsValid);
    }

    [Fact]
    public async Task Horario_DiaCerrado_NoExigeQueLaAperturaSeaAnteriorAlCierre()
    {
        var resultado = await Horario.ValidateAsync(new ActualizarHorarioRequest(false, new TimeOnly(0, 0), new TimeOnly(0, 0)));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public async Task DiaCerrado_FechaDeHoyEnAdelante_YMotivoHasta200Caracteres()
    {
        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)); // 1/12 12:00 local
        var validador = new CrearDiaCerradoRequestValidator(reloj);

        var hoy = await validador.ValidateAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 1), null));
        var ayer = await validador.ValidateAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 11, 30), null));
        var motivoLargo = await validador.ValidateAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), new string('x', 201)));
        var motivoLimite = await validador.ValidateAsync(new CrearDiaCerradoRequest(new DateOnly(2026, 12, 25), new string('x', 200)));
        var sinFecha = await validador.ValidateAsync(new CrearDiaCerradoRequest(default, null));

        Assert.True(hoy.IsValid);
        Assert.Equal("La fecha no puede ser en el pasado.", Assert.Single(ayer.Errors).ErrorMessage);
        Assert.Equal("El motivo no puede superar los 200 caracteres.", Assert.Single(motivoLargo.Errors).ErrorMessage);
        Assert.True(motivoLimite.IsValid);
        Assert.Contains(sinFecha.Errors, e => e.ErrorMessage == "La fecha es obligatoria.");
    }
}
