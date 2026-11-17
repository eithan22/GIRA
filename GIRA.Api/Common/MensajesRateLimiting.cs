namespace GIRA.Api.Common;

/// <summary>Mensajes de rechazo (429) compartidos entre los módulos que limitan intentos.</summary>
public static class MensajesRateLimiting
{
    /// <summary>Mensaje neutro: no menciona el módulo ni el motivo, igual para todos los endpoints.</summary>
    public const string Neutro = "Demasiadas solicitudes. Espera un minuto e inténtalo de nuevo.";
}
