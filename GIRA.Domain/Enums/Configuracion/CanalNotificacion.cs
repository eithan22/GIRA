namespace GIRA.Domain.Enums.Configuracion;

/// <summary>Canal de una notificación. Se guarda como número: no cambiarlos sin migración.</summary>
public enum CanalNotificacion
{
    /// <summary>Dentro de la aplicación (bandeja y tiempo real). El correo se descartó en el SRS v1.3.</summary>
    InApp = 0
}
