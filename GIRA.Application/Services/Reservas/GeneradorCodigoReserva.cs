using System.Security.Cryptography;

namespace GIRA.Application.Services.Reservas;

/// <summary>Genera el código de 8 dígitos con el que el cliente consulta o cancela su reserva.</summary>
public class GeneradorCodigoReserva
{
    /// <summary>Genera 8 dígitos como texto (con ceros a la izquierda si hace falta).</summary>
    public string Generar() => RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
}
