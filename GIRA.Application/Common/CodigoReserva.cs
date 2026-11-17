using System.Text.RegularExpressions;

namespace GIRA.Application.Common;

/// <summary>
/// Normalización y validación del código de reserva de 8 dígitos (GI-401), como <see cref="TelefonoRd"/> hace con
/// el teléfono. El código es texto: los ceros a la izquierda ("00001234") son parte de él.
/// </summary>
public static class CodigoReserva
{
    // [0-9] y no \d: \d también acepta dígitos Unicode (por ejemplo árabes o devanagari) que nunca son un código
    // generado. \z (y no $) para que un salto de línea final tampoco pase por válido.
    private static readonly Regex FormatoValido = new(@"^[0-9]{8}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Quita los espacios (cualquier espacio en blanco) y los guiones. No toca nada más.</summary>
    public static string Normalizar(string codigo)
    {
        return new string(codigo.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
    }

    /// <summary>Exactamente 8 dígitos ASCII (0 a 9), sobre un código ya normalizado.</summary>
    public static bool EsValido(string codigoNormalizado) => FormatoValido.IsMatch(codigoNormalizado);
}
