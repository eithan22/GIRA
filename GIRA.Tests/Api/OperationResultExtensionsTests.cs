using GIRA.Api.Common;
using GIRA.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Tests.Api;

/// <summary>Pruebas de ToActionResult.</summary>
public class OperationResultExtensionsTests
{
    // Éxito -> 204.
    [Fact]
    public void ToActionResult_Exitoso_Devuelve204()
    {
        var resultado = OperationResult.Success();

        var accionResult = resultado.ToActionResult();

        var sinContenido = Assert.IsType<NoContentResult>(accionResult);
        Assert.Equal(StatusCodes.Status204NoContent, sinContenido.StatusCode);
    }

    // Éxito con valor -> 200.
    [Fact]
    public void ToActionResultConValor_Exitoso_Devuelve200ConValor()
    {
        var resultado = OperationResult.Success("hola");

        var accionResult = resultado.ToActionResult();

        var ok = Assert.IsType<OkObjectResult>(accionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal("hola", ok.Value);
    }

    // Cada ErrorType mapea a su código HTTP.
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    public void ToActionResult_Fallido_DevuelveProblemDetailsConCodigoCorrecto(ErrorType tipo, int codigoEsperado)
    {
        var resultado = OperationResult.Failure(tipo, "detalle del error");

        var accionResult = resultado.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(accionResult);
        Assert.Equal(codigoEsperado, objectResult.StatusCode);
        var problema = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(codigoEsperado, problema.Status);
        Assert.Equal("detalle del error", problema.Detail);
    }

    // Igual, para ToActionResult<T>.
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    public void ToActionResultConValor_Fallido_DevuelveProblemDetailsConCodigoCorrecto(ErrorType tipo, int codigoEsperado)
    {
        var resultado = OperationResult.Failure<int>(tipo, "detalle del error");

        var accionResult = resultado.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(accionResult);
        Assert.Equal(codigoEsperado, objectResult.StatusCode);
        var problema = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(codigoEsperado, problema.Status);
        Assert.Equal("detalle del error", problema.Detail);
    }
}
