using GIRA.Application.DTOs.Seguridad.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GIRA.Tests.Api;

/// <summary>Atajos para las pruebas de integración que necesitan iniciar sesión y mandar el token.</summary>
public static class ClienteHttpExtensions
{
    /// <summary>Inicia sesión con POST /api/auth/login y devuelve el access token.</summary>
    public static async Task<string> ObtenerTokenAsync(this HttpClient cliente, string correo, string password)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = correo, Password = password });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<LoginResponse>();
        return cuerpo!.AccessToken;
    }

    /// <summary>Manda el token en el encabezado <c>Authorization: Bearer</c> de todas las peticiones siguientes.</summary>
    public static void UsarToken(this HttpClient cliente, string token)
    {
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
