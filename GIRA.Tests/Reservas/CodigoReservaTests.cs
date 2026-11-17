using GIRA.Application.Common;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas del helper <see cref="CodigoReserva"/> (GI-401): normalización y formato de 8 dígitos ASCII.</summary>
public class CodigoReservaTests
{
    [Theory]
    [InlineData("12345678", "12345678")]
    [InlineData("1234 5678", "12345678")]
    [InlineData("1234-5678", "12345678")]
    [InlineData("  1234 - 5678  ", "12345678")]
    [InlineData("\t1234\n5678\r", "12345678")]
    [InlineData("0000 1234", "00001234")]
    [InlineData("", "")]
    [InlineData("12.34", "12.34")] // solo quita espacios y guiones; lo demás lo rechaza EsValido
    public void Normalizar_QuitaEspaciosYGuionesYNadaMas(string escrito, string esperado)
    {
        Assert.Equal(esperado, CodigoReserva.Normalizar(escrito));
    }

    [Theory]
    [InlineData("00000000")]
    [InlineData("12345678")]
    [InlineData("99999999")]
    public void EsValido_Acepta8DigitosAscii(string codigo)
    {
        Assert.True(CodigoReserva.EsValido(codigo));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567A")]
    [InlineData("ABCDEFGH")]
    [InlineData("1234 5678")]       // sin normalizar
    [InlineData("12345678\n")]      // \z: un salto de línea final no pasa
    [InlineData("١٢٣٤٥٦٧٨")]       // dígitos árabe-índicos: \d los aceptaría, [0-9] no
    [InlineData("１２３４５６７８")]  // dígitos de ancho completo
    [InlineData("१२३४५६७८")]       // dígitos devanagari
    public void EsValido_RechazaTodoLoQueNoSeaExactamente8DigitosAscii(string codigo)
    {
        Assert.False(CodigoReserva.EsValido(codigo));
    }

    [Fact]
    public void Un_Digito_Unicode_Nunca_Llega_A_Ser_Valido_Aunque_Se_Normalice()
    {
        Assert.False(CodigoReserva.EsValido(CodigoReserva.Normalizar("١٢٣٤ ٥٦٧٨")));
    }
}
