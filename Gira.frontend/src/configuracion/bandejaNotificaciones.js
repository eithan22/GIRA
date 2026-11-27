/** Suma una notificación recibida en tiempo real sin duplicarla (puede llegar mientras se recarga la bandeja). */
export function agregarNotificacion(bandeja, nueva) {
  if (bandeja.notificaciones.some((n) => n.id === nueva.id)) return bandeja;
  return {
    notificaciones: [nueva, ...bandeja.notificaciones],
    noLeidas: bandeja.noLeidas + (nueva.leida ? 0 : 1),
  };
}

/** Marca una como leída en la bandeja local. */
export function marcarLeidaLocal(bandeja, id) {
  const objetivo = bandeja.notificaciones.find((n) => n.id === id);
  if (!objetivo || objetivo.leida) return bandeja;
  return {
    notificaciones: bandeja.notificaciones.map((n) => (n.id === id ? { ...n, leida: true } : n)),
    noLeidas: Math.max(0, bandeja.noLeidas - 1),
  };
}

/** Fecha corta en hora de Santo Domingo: «26/11, 14:05». */
export function fechaCorta(iso) {
  // El servidor guarda UTC sin la «Z»; se agrega para que el navegador no la lea como hora local.
  const fecha = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
  return fecha.toLocaleString('es-DO', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'America/Santo_Domingo' });
}
