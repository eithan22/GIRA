using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Tests.Common;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas de <see cref="CrearReservaRequestValidator"/> enfocadas en el teléfono: los casos
/// con código de país deben dar un mensaje distinto del de formato inválido genérico.
/// </summary>
public class CrearReservaRequestValidatorTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 25);

    private static CrearReservaRequestValidator CrearValidador()
    {
        var parametros = new ParametrosReservaFalso();
        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero));
        return new CrearReservaRequestValidator(parametros, reloj);
    }

    [Theory]
    [InlineData("+1 809 555 1234")]
    [InlineData("1-809-555-1234")]
    public async Task Telefono_ConCodigoDePais_DaMensajeDeEscribirSolo10Digitos(string telefono)
    {
        var validador = CrearValidador();
        var request = new CrearReservaRequest("Ana", telefono, Fecha, Guid.NewGuid(), 2);

        var resultado = await validador.ValidateAsync(request);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e =>
            e.PropertyName == nameof(CrearReservaRequest.Telefono)
            && e.ErrorMessage.Contains("sin código de país"));
    }

    [Fact]
    public async Task Telefono_10DigitosValido_NoDaErrorDeTelefono()
    {
        var validador = CrearValidador();
        var request = new CrearReservaRequest("Ana", "8095551234", Fecha, Guid.NewGuid(), 2);

        var resultado = await validador.ValidateAsync(request);

        Assert.DoesNotContain(resultado.Errors, e => e.PropertyName == nameof(CrearReservaRequest.Telefono));
    }

    [Fact]
    public async Task Telefono_10DigitosConCodigoDeAreaInvalido_DaMensajeDeFormatoGeneral()
    {
        var validador = CrearValidador();
        var request = new CrearReservaRequest("Ana", "5551234567", Fecha, Guid.NewGuid(), 2);

        var resultado = await validador.ValidateAsync(request);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e =>
            e.PropertyName == nameof(CrearReservaRequest.Telefono)
            && e.ErrorMessage.Contains("809, 829 o 849")
            && !e.ErrorMessage.Contains("código de país"));
    }
}
