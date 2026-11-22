using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GIRA.Application.Events.Analitica;

namespace GIRA.Application.Common.Personal;


/// <summary>
/// Identificador PROVISIONAL de una predicción, derivado del contenido del evento (GI-303).
/// El evento de Analítica todavía no trae un identificador; cuando Isaac lo confirme, este cálculo se reemplaza.
/// Un mismo contenido produce siempre el mismo identificador, así que un evento repetido no genera sugerencias
/// duplicadas. Cambiar cualquier fecha, franja o afluencia produce otro identificador.
/// </summary>
public static class IdentificadorPrediccionProvisional
{
    /// <summary>Calcula el identificador a partir del rango y de las predicciones, sin importar su orden.</summary>
    public static Guid Derivar(PrediccionAfluenciaGeneradaEvent evento)
    {
        var detalle = evento.Predicciones
            .OrderBy(p => p.Fecha)
            .ThenBy(p => p.FranjaHorariaId)
            .ThenBy(p => p.AfluenciaEstimada)
            .Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Fecha:yyyy-MM-dd},{p.FranjaHorariaId:N},{p.AfluenciaEstimada}"));

        var canonico = string.Create(CultureInfo.InvariantCulture, $"{evento.Desde:yyyy-MM-dd}|{evento.Hasta:yyyy-MM-dd}|") + string.Join(";", detalle);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonico));
        return new Guid(hash.AsSpan(0, 16));
    }
}
