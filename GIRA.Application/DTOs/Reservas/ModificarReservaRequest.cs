namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos para que el personal modifique una reserva (GI-160). El nombre y el teléfono del cliente
/// no se modifican. Si <paramref name="MesaId"/> es null se mantiene la mesa actual cuando todavía
/// sirve; si no, se asigna la de menor capacidad que alcance (RN-RES-13).
/// </summary>
/// <param name="Fecha">Nueva fecha (o la misma).</param>
/// <param name="FranjaHorariaId">Nueva franja (o la misma).</param>
/// <param name="Personas">Nueva cantidad de personas (o la misma).</param>
/// <param name="MesaId">Mesa elegida por el personal; opcional.</param>
public record ModificarReservaRequest(DateOnly Fecha, Guid FranjaHorariaId, int Personas, Guid? MesaId = null);
