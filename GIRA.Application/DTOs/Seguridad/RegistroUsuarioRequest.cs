namespace GIRA.Application.DTOs.Seguridad;

/// <summary>Datos para solicitar una cuenta de personal.</summary>
public class RegistroUsuarioRequest
{
    public string NombreCompleto { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
