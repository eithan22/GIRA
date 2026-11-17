using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos para crear una reserva. Sin UsuarioId ni Estado: el usuario que registra se toma
/// del token (BaseEntity.CreatedBy, vía AuditoriaSaveChangesInterceptor), nunca del cuerpo.
/// Origen es opcional: el cliente anónimo no lo manda (y si lo manda, se ignora); el
/// personal autenticado debe mandar Telefono o Presencial (GI-222/GI-235).
/// </summary>
/// <param name="ZonaId">
/// Zona preferida (GI-4xx); null = sin preferencia. Solo la usa el canal web (<see cref="OrigenReserva.Web"/>); el
/// canal del personal usa la asignación automática de siempre y rechaza este campo con ZONA_NO_APLICA_PERSONAL si
/// llega con un valor.
/// </param>
public record CrearReservaRequest(
    string Nombre, string Telefono, DateOnly Fecha, Guid FranjaHorariaId, int Personas,
    OrigenReserva? Origen = null, Guid? ZonaId = null);
