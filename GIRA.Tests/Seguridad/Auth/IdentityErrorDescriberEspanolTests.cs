using GIRA.Infrastructure.Identity;

namespace GIRA.Tests.Seguridad.Auth;

public class IdentityErrorDescriberEspanolTests
{
    private readonly IdentityErrorDescriberEspanol _descriptor = new();

    [Fact]
    public void PasswordTooShort_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordTooShort(8);

        Assert.Equal("La contraseña debe tener al menos 8 caracteres.", error.Description);
    }

    [Fact]
    public void PasswordRequiresUpper_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordRequiresUpper();

        Assert.Equal("La contraseña debe tener al menos una mayúscula.", error.Description);
    }

    [Fact]
    public void PasswordRequiresLower_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordRequiresLower();

        Assert.Equal("La contraseña debe tener al menos una minúscula.", error.Description);
    }

    [Fact]
    public void PasswordRequiresDigit_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordRequiresDigit();

        Assert.Equal("La contraseña debe tener al menos un número.", error.Description);
    }

    [Fact]
    public void PasswordRequiresNonAlphanumeric_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordRequiresNonAlphanumeric();

        Assert.Equal("La contraseña debe tener al menos un símbolo.", error.Description);
    }

    [Fact]
    public void PasswordMismatch_DevuelveMensajeEnEspanol()
    {
        var error = _descriptor.PasswordMismatch();

        Assert.Equal("La contraseña actual es incorrecta.", error.Description);
    }
}
