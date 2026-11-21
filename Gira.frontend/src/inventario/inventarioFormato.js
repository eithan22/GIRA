// Funciones puras del módulo de Inventario (formato, roles, armado de peticiones). Sin React ni axios,
// para poder probarlas con `node --test`.

export const ROLES_CONSULTAR = ['Administrador', 'Gerente', 'Supervisor', 'Cocina'];
export const ROLES_ATENDER = ['Administrador', 'Gerente', 'Supervisor'];
export const ROLES_GESTIONAR = ['Administrador', 'Gerente'];

export const NIVELES = { Normal: 'Normal', Bajo: 'Bajo', Critico: 'Crítico' };
export const TIPOS_MOVIMIENTO = { Entrada: 'Entrada', Salida: 'Salida (consumo)', Merma: 'Merma', Ajuste: 'Ajuste' };
export const TIPOS_ALERTA = {
  StockBajo: 'Stock bajo',
  QuiebreProyectado: 'Se acabará pronto',
  Vencimiento: 'Por vencer',
  Inconsistencia: 'Inconsistencia',
};
export const ESTADOS_ALERTA = { Abierta: 'Abierta', Atendida: 'Atendida', Descartada: 'Descartada' };

const ZONA = 'America/Santo_Domingo';

/** true si el usuario tiene alguno de los roles. */
export function tieneRol(user, roles) {
  return roles.some((rol) => user?.roles?.includes(rol));
}

/** Número con hasta 3 decimales, en formato dominicano. '—' si no hay valor. */
export function numero(valor, decimales = 3) {
  if (valor === null || valor === undefined || valor === '') return '—';
  return Number(valor).toLocaleString('es-DO', { maximumFractionDigits: decimales });
}

/** Monto en pesos dominicanos. */
export function dinero(valor) {
  if (valor === null || valor === undefined) return '—';
  return `RD$ ${Number(valor).toLocaleString('es-DO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

/** 'AAAA-MM-DD' → 'DD/MM/AAAA' sin pasar por Date (evita que la zona horaria mueva el día). */
export function fecha(iso) {
  if (!iso) return '—';
  const [a, m, d] = String(iso).slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

/** Fecha y hora de un instante UTC, en hora de Santo Domingo. */
export function fechaHora(isoUtc) {
  if (!isoUtc) return '—';
  return new Date(isoUtc).toLocaleString('es-DO', {
    timeZone: ZONA, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

/** Hoy en Santo Domingo, como 'AAAA-MM-DD'. */
export function hoyLocal(ahora = new Date()) {
  return ahora.toLocaleDateString('en-CA', { timeZone: ZONA });
}

/** Suma días a una fecha 'AAAA-MM-DD' (en UTC, así no influye el horario de verano). */
export function sumarDias(iso, dias) {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
}

/** Convierte lo escrito en un campo numérico; null si está vacío o no es número. */
export function aNumero(texto) {
  if (texto === null || texto === undefined || String(texto).trim() === '') return null;
  const valor = Number(String(texto).replace(',', '.'));
  return Number.isFinite(valor) ? valor : null;
}

/**
 * Cuerpo de POST /api/movimientos. En un ajuste, `sentido` dice si suma ('+') o resta ('-');
 * en entrada y merma la cantidad siempre va positiva (la merma la resta el servidor).
 */
export function armarMovimiento({ insumoId, tipo, cantidad, sentido = '+', costoUnitario, motivo, referencia, fechaVencimiento }) {
  const valor = Math.abs(aNumero(cantidad) ?? 0);
  return {
    insumoId,
    tipo,
    cantidad: tipo === 'Ajuste' && sentido === '-' ? -valor : valor,
    costoUnitario: tipo === 'Entrada' ? aNumero(costoUnitario) : null,
    motivo: motivo?.trim() || null,
    referencia: referencia?.trim() || null,
    fechaVencimiento: tipo === 'Entrada' && fechaVencimiento ? fechaVencimiento : null,
  };
}

/** Texto del error para mostrar: el `detail` del servidor en 400/404/409, o uno genérico. */
export function mensajeError(error, porDefecto = 'No se pudo completar la acción. Intenta de nuevo.') {
  const estado = error?.response?.status;
  if (estado === 403) return 'No tienes permiso para hacer esto.';
  const datos = error?.response?.data;
  if ([400, 404, 409].includes(estado)) {
    if (typeof datos?.detail === 'string' && datos.detail.trim()) return datos.detail;
    const primero = datos?.errors && Object.values(datos.errors).flat().find((m) => typeof m === 'string');
    if (primero) return primero;
  }
  return porDefecto;
}

/** Quita los parámetros vacíos para no ensuciar el query string. */
export function limpiar(params) {
  return Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));
}
