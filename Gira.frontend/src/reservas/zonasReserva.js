// Zona opcional en /reservar (GI-4xx). Lógica pura, sin React ni red, para probarla con `node --test`.
// El servidor manda en cada franja `zonasDisponibles` ([{ id, nombre }]): solo las zonas con una mesa libre de
// capacidad suficiente en esa franja. Las zonas llenas NO se ofrecen (decisión del equipo: se ocultan).

import { ETIQUETA_SIN_PREFERENCIA } from './reservasTextos.js';

/** Zonas con mesa libre en la franja; [] si el servidor no manda el campo o la franja no está disponible. */
export function zonasDeFranja(franja) {
  const zonas = franja?.zonasDisponibles;
  return Array.isArray(zonas) ? zonas : [];
}

/** Solo hay que preguntar cuando existe una decisión real: más de una zona con mesa libre. */
export function hayEleccionDeZona(franja) {
  return zonasDeFranja(franja).length > 1;
}

/**
 * Opciones del selector: "Sin preferencia" primero (valor '') y una por zona disponible. Vacío si no hay
 * decisión que tomar (una sola zona o ninguna).
 */
export function opcionesSelectorZona(franja) {
  if (!hayEleccionDeZona(franja)) return [];
  return [
    { valor: '', etiqueta: ETIQUETA_SIN_PREFERENCIA },
    ...zonasDeFranja(franja).map((zona) => ({ valor: zona.id, etiqueta: zona.nombre })),
  ];
}

/** La zona elegida (null = sin preferencia) sigue ofreciéndose en esa franja. */
export function zonaSigueDisponible(zonaId, franja) {
  return !zonaId || zonasDeFranja(franja).some((zona) => zona.id === zonaId);
}

/** Nombre de la zona elegida para el resumen; null si no hay zona elegida o ya no existe en la franja. */
export function nombreZonaElegida(franja, zonaId) {
  if (!zonaId) return null;
  return zonasDeFranja(franja).find((zona) => zona.id === zonaId)?.nombre ?? null;
}
