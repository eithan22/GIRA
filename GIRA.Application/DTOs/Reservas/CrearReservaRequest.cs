using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos para crear una reserva. Sin UsuarioId ni Estado: el usuario que registra se toma
/// del token (BaseEntity.CreatedBy, vía AuditoriaSaveChangesInterceptor), nunca del cuerpo.
/// Origen es opcional: el cliente anónimo no lo manda (y si lo manda, se ignora); el
/// personal autenticado debe mandar Telefono o Presencial (GI-222/GI-235).
/// </summary>
public record CrearReservaRequest(
    string Nombre, string Telefono, DateOnly Fecha, Guid FranjaHorariaId, int Personas,
    OrigenReserva? Origen = null);
