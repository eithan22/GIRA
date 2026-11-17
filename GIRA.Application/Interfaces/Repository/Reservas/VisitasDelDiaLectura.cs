namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>
/// Una visita sin reserva con su franja y su mesa ya resueltas (GI-402). <see cref="FranjaNombre"/> y
/// <see cref="MesaNumero"/> son null si la franja o la mesa ya no existen (borrado lógico): la visita no desaparece
/// de la lista por eso.
/// </summary>
/// <param name="Id">Identificador de la visita.</param>
/// <param name="FranjaNombre">Nombre de la franja, o null si ya no existe.</param>
/// <param name="MesaNumero">Número de la mesa; null si no tiene mesa o si ya no existe.</param>
/// <param name="Personas">Personas atendidas.</param>
/// <param name="CreadaEnUtc">Momento UTC del registro (BaseEntity.CreatedAt); default si no tiene auditoría.</param>
public sealed record VisitaListadoLectura(Guid Id, string? FranjaNombre, int? MesaNumero, int Personas, DateTime CreadaEnUtc);

/// <summary>
/// Visitas de un día: totales de todo el día y las filas más recientes hasta el tope pedido (GI-402).
/// </summary>
/// <param name="TotalVisitas">Todas las visitas del día, aunque no estén en <paramref name="Filas"/>.</param>
/// <param name="TotalPersonas">Suma de las personas de todas las visitas del día.</param>
/// <param name="Filas">Las más recientes primero, hasta el tope.</param>
public sealed record VisitasDelDiaLectura(int TotalVisitas, int TotalPersonas, IReadOnlyList<VisitaListadoLectura> Filas);
