
namespace GIRA.Application.DTOs.Seguridad;

/// <summary>
/// Solicitud para cambiar la contraseña de un usuario autenticado.
/// </summary>
public class CambiarPasswordRequest
{
    public string PasswordActual { get; set; } = string.Empty;

    public string PasswordNueva { get; set; } = string.Empty;
}
