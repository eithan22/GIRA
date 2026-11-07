using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Cliente identificado solo por nombre y teléfono; no tiene cuenta ni email.
/// Se busca o se crea por teléfono al registrar una reserva.
/// </summary>
public class Cliente : BaseEntity
{
    /// <summary>Nombre completo del cliente, tal como lo dio al reservar.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Teléfono de 10 dígitos de República Dominicana (809, 829 o 849), guardado solo con números.</summary>
    public string Telefono { get; set; } = string.Empty;
}
