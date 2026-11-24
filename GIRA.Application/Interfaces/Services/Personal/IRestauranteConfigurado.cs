namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>
/// Restaurante configurado para este despliegue (Analitica:RestauranteId). Null si no está configurado o no es un GUID válido.
/// Lo usa Personal para validar que un evento de predicción sea de este restaurante (GI-303).
/// </summary>
public interface IRestauranteConfigurado
{
    /// <summary>Identificador del restaurante configurado, o null.</summary>
    Guid? RestauranteId { get; }
}
