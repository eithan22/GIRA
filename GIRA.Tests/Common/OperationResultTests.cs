using GIRA.Application.Common;

namespace GIRA.Tests.Common;

/// <summary>Pruebas de OperationResult.</summary>
public class OperationResultTests
{
    // Success(): exitoso, sin tipo ni mensaje.
    [Fact]
    public void Success_DevuelveIsSuccessTrue()
    {
        var resultado = OperationResult.Success();

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.IsFailure);
        Assert.Null(resultado.Type);
        Assert.Null(resultado.Error);
    }

    // Success<T>: guarda el valor.
    [Fact]
    public void SuccessConValor_GuardaElValor()
    {
        var resultado = OperationResult.Success(42);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(42, resultado.Value);
    }

    // Failure: guarda tipo y mensaje, para cada ErrorType.
    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    public void Failure_PorCadaErrorType_GuardaTipoYMensaje(ErrorType tipo)
    {
        var resultado = OperationResult.Failure(tipo, "mensaje de error");

        Assert.Equal(tipo, resultado.Type);
        Assert.Equal("mensaje de error", resultado.Error);
    }

    // Failure nunca deja IsSuccess en true.
    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    public void Failure_NuncaTieneIsSuccessEnTrue(ErrorType tipo)
    {
        var resultado = OperationResult.Failure(tipo, "mensaje de error");

        Assert.False(resultado.IsSuccess);
        Assert.True(resultado.IsFailure);
    }

    // Failure<T>: igual queda fallido, Value en default.
    [Fact]
    public void FailureConValor_NuncaTieneIsSuccessEnTrue()
    {
        var resultado = OperationResult.Failure<int>(ErrorType.NotFound, "no encontrado");

        Assert.False(resultado.IsSuccess);
        Assert.True(resultado.IsFailure);
        Assert.Equal(0, resultado.Value);
    }
}
