namespace GIRA.Domain.Enums.Reservas;

/// <summary>
/// Canal por el que se registró la reserva.
/// </summary>
public enum OrigenReserva
{
    /// <summary>El cliente reservó por su cuenta desde el sitio web público.</summary>
    Web,

    /// <summary>El personal registró la reserva a partir de una llamada telefónica.</summary>
    Telefono,

    /// <summary>El personal registró la reserva con el cliente presente en el restaurante.</summary>
    Presencial
}
