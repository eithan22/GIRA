namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Una visita sin reserva en el listado del día (GI-402). Sin datos personales: no hay cliente, teléfono ni
/// usuario que registra; solo franja, mesa opcional, personas y la hora de registro.
/// </summary>
/// <param name="Id">Identificador de la visita (para que la lista tenga una clave estable).</param>
/// <param name="FranjaNombre">Nombre de la franja en la que ocurrió.</param>
/// <param name="MesaNumero">Número de la mesa; null si la visita se registró sin mesa.</param>
/// <param name="Personas">Personas atendidas.</param>
/// <param name="HoraRegistro">Hora local de Santo Domingo en que se registró; null solo si el registro no tiene auditoría.</param>
public record VisitaListadoDto(Guid Id, string FranjaNombre, int? MesaNumero, int Personas, TimeOnly? HoraRegistro);
