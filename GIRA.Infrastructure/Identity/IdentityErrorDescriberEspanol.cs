using Microsoft.AspNetCore.Identity;

namespace GIRA.Infrastructure.Identity;

/// <summary>Traduce al español los mensajes de IdentityErrorDescriber usados por GIRA (política de contraseña).</summary>
public class IdentityErrorDescriberEspanol : IdentityErrorDescriber
{
    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"La contraseña debe tener al menos {length} caracteres."
    };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "La contraseña debe tener al menos una mayúscula."
    };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "La contraseña debe tener al menos una minúscula."
    };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "La contraseña debe tener al menos un número."
    };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "La contraseña debe tener al menos un símbolo."
    };

    /// <inheritdoc />
    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "La contraseña actual es incorrecta."
    };
}
