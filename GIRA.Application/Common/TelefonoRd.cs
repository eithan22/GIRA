using System.Text.RegularExpressions;

namespace GIRA.Application.Common;

/// <summary>
/// Normalización y validación de teléfonos de República Dominicana (809, 829, 849).
/// </summary>
public static class TelefonoRd
{
    private static readonly Regex FormatoValido = new(@"^(809|829|849)\d{7}$", RegexOptions.Compiled);

    /// <summary>Quita espacios, guiones y paréntesis. No quita ningún "1" inicial ni el "+".</summary>
    public static string Normalizar(string telefono)
    {
        return new string(telefono.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());
    }

    /// <summary>10 dígitos empezando por 809, 829 o 849.</summary>
    public static bool EsValido(string telefonoNormalizado) => FormatoValido.IsMatch(telefonoNormalizado);

    /// <summary>
    /// Detecta "+1" o un "1" inicial seguido de 10 dígitos (ej. "+18095551234" o "18095551234"),
    /// para dar un mensaje específico distinto del de formato inválido genérico.
    /// </summary>
    public static bool EmpiezaConCodigoDePais(string telefonoNormalizado)
    {
        return telefonoNormalizado.StartsWith("+1", StringComparison.Ordinal)
            || (telefonoNormalizado.Length == 11 && telefonoNormalizado.StartsWith('1'));
    }
}
