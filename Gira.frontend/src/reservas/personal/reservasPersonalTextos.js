import { ESTADOS_RESERVA } from '../reservasTextos.js';

// Textos y mapas del personal. Lo genérico (colores de estado, mensaje de error
// genérico) se reutiliza de reservasTextos.js sin modificarlo.

export const MENSAJE_SIN_PERMISO = 'Tu rol no tiene permiso para realizar esta acción.';
export const MENSAJE_SESION_EXPIRADA = 'Tu sesión terminó. Inicia sesión nuevamente.';

export const TEXTO_CARGANDO = 'Cargando…';
export const TEXTO_PROXIMAMENTE = 'Próximamente';
export const TEXTO_REINTENTAR = 'Reintentar';

// Nombres de las pantallas: se usan en el h1 y en document.title.
export const PANTALLAS = {
  agenda: {
    titulo: 'Agenda de reservas',
    eyebrow: 'RESERVAS',
    descripcion: 'Reservas del día con filtros, búsqueda por teléfono y acciones de check-in, completar y cancelar.',
  },
  mapa: {
    titulo: 'Mapa de mesas',
    eyebrow: 'RESERVAS',
    descripcion: 'Estado de cada mesa por fecha y franja: libre, reservada, ocupada o inactiva.',
  },
  visitas: {
    titulo: 'Visitas sin reserva',
    eyebrow: 'RESERVAS',
    descripcion: 'Registro de clientes que llegan sin reserva.',
  },
  configuracion: {
    titulo: 'Zonas, mesas y franjas',
    eyebrow: 'CONFIGURACIÓN DEL SALÓN',
    descripcion: 'Administración de zonas, mesas y franjas horarias (solo Administrador y Gerente).',
  },
};

// Etiquetas para el personal. Los colores salen de ESTADOS_RESERVA (Eithan);
// solo cambia el texto donde el de cliente no sirve: «No asististe» pasa a
// «No asistió» porque el personal habla del cliente en tercera persona.
export const ESTADOS_RESERVA_PERSONAL = {
  Confirmada: { ...ESTADOS_RESERVA.Confirmada, etiqueta: 'Confirmada' },
  EnCurso: { ...ESTADOS_RESERVA.EnCurso, etiqueta: 'En curso' },
  Completada: { ...ESTADOS_RESERVA.Completada, etiqueta: 'Completada' },
  Cancelada: { ...ESTADOS_RESERVA.Cancelada, etiqueta: 'Cancelada' },
  NoAsistio: { ...ESTADOS_RESERVA.NoAsistio, etiqueta: 'No asistió' },
};

// --- Agenda (Lote 2, GI-394) ---

// Mismo texto que ListarReservasRequestValidator.cs: el cliente nunca dice algo distinto al servidor.
export const MENSAJE_TELEFONO_INVALIDO = 'El teléfono debe tener 10 dígitos y empezar con 809, 829 o 849.';

export const TEXTO_BUSQUEDA_TODAS_LAS_FECHAS = 'La búsqueda por teléfono muestra todas las fechas.';
export const TEXTO_SIN_RESULTADOS_TELEFONO = 'No encontramos reservas para ese teléfono.';
export const TEXTO_FRANJAS_NO_DISPONIBLES = 'No pudimos cargar las franjas; el filtro por franja no está disponible.';
export const TEXTO_FILTRO_TODOS = 'Todos';
export const TEXTO_FILTRO_TODAS = 'Todas';
export const TEXTO_FRANJA_INACTIVA = 'inactiva';

export function textoSinReservas(fechaLarga, conFiltros) {
  return conFiltros
    ? `No hay reservas con esos filtros para el ${fechaLarga}.`
    : `No hay reservas para el ${fechaLarga}.`;
}

export function textoConteo(total, porTelefono, fechaLarga) {
  const reservas = total === 1 ? '1 reserva' : `${total} reservas`;
  return porTelefono ? `${reservas} encontradas por teléfono` : `${reservas} · ${fechaLarga}`;
}

// --- Detalle y acciones ---

export const TEXTO_DETALLE_NO_CARGO = 'No pudimos cargar el detalle de la reserva.';
export const TEXTO_RESERVA_NO_EXISTE = 'Esta reserva ya no existe. La lista se actualizó.';
export const TEXTO_CARGANDO_DATOS_MESA = 'Cargando los datos de la mesa…';

export const TEXTO_ACCION_CHECKIN = 'Check-in';
export const TEXTO_ACCION_COMPLETAR = 'Completar visita';
export const TEXTO_ACCION_CANCELAR = 'Cancelar reserva';

export const TEXTO_CONFIRMAR_CHECKIN = 'Confirmar check-in';
export const TEXTO_CONFIRMAR_COMPLETAR = 'Sí, completar visita';
export const TEXTO_CONFIRMAR_CANCELAR = 'Sí, cancelar reserva';
export const TEXTO_VOLVER = 'Volver';
export const TEXTO_MANTENER_RESERVA = 'Mantener reserva';

export const TITULO_CHECKIN = '¿Cuántas personas llegaron?';
export const CUERPO_CHECKIN = 'Se registra la llegada y la reserva pasa a En curso.';
export const TITULO_COMPLETAR = '¿Completar la visita?';
export const CUERPO_COMPLETAR = 'Se registra la visita como completada y la mesa queda libre.';
export const TITULO_CANCELAR = '¿Cancelar esta reserva?';
export const CUERPO_CANCELAR = 'La mesa quedará libre y esta acción no se puede deshacer. El motivo queda guardado en la reserva.';

export const EXITO_CHECKIN = 'Check-in registrado. La reserva está En curso.';
export const EXITO_COMPLETAR = 'Visita completada.';
export const EXITO_CANCELAR = 'Reserva cancelada.';

// Solo informa lo que pasó; no promete ningún flujo posterior.
export const AVISO_NO_ASISTIO = 'El cliente no llegó dentro de la tolerancia, por eso la reserva quedó como no asistida.';

export const ETIQUETA_PERSONAS_REALES = 'Personas que llegaron';
export const ETIQUETA_MOTIVO = 'Motivo de la cancelación';
export const AYUDA_MOTIVO = 'Obligatorio. Entre 1 y 500 caracteres.';

export const ETIQUETA_ACTUALIZADO = 'Actualizado a las';

// --- Nueva reserva y modificar (Lote 3, GI-395) ---
// Los mensajes del servidor se muestran tal cual (algunos están redactados con voz de cliente).

export const TEXTO_NUEVA_RESERVA = 'Nueva reserva';
export const TEXTO_ORIGEN_LEYENDA = '¿Cómo llegó esta reserva?';
// Única fuente de las etiquetas del origen (formulario, agenda y detalle); el valor guardado sigue siendo Telefono/Presencial.
export const TEXTO_ORIGEN_TELEFONO = 'Por llamada';
export const TEXTO_ORIGEN_PRESENCIAL = 'En persona';
export const AYUDA_ORIGEN_TELEFONO = 'El cliente llamó por teléfono.';
export const AYUDA_ORIGEN_PRESENCIAL = 'El cliente está en el restaurante ahora.';
export const MENSAJE_ORIGEN_OBLIGATORIO = 'Elige cómo llegó la reserva.';
export const MENSAJE_FECHA_FUERA_DE_RANGO = 'Elige una fecha desde hoy hasta 30 días adelante.';
export const MENSAJE_FRANJA_OBLIGATORIA = 'Elige una franja con disponibilidad.';

export const TEXTO_MESA_AUTOMATICA_CREAR = 'La mesa se asigna automáticamente: la de menor capacidad que alcance.';
export const TEXTO_CREAR_RESERVA = 'Crear reserva';
export const TEXTO_CREANDO = 'Creando…';
export const TEXTO_CANCELAR_FORMULARIO = 'Cancelar';

// Buscador de cliente existente
export const ETIQUETA_BUSCAR_CLIENTE = 'Cliente existente (opcional)';
export const AYUDA_BUSCAR_CLIENTE = 'Escribe al menos 3 caracteres del nombre o del teléfono.';
export const TEXTO_BUSCANDO_CLIENTES = 'Buscando clientes…';
export const TEXTO_SIN_CLIENTES = 'No hay clientes con ese texto. Puedes escribir los datos abajo.';
export const TEXTO_USAR_CLIENTE = 'Usar';
export const AVISO_NOMBRE_SE_ACTUALIZA = 'Al guardar, el nombre del cliente se actualizará.';

// Disponibilidad de franjas
export const TEXTO_ELIGE_FECHA_Y_PERSONAS = 'Elige una fecha y las personas para ver la disponibilidad.';
export const TEXTO_CONSULTANDO_DISPONIBILIDAD = 'Consultando disponibilidad…';
export const TEXTO_SIN_FRANJAS_ACTIVAS = 'No hay franjas activas configuradas.';
export const TEXTO_SIN_DISPONIBILIDAD_PERSONAL = 'No hay franjas con disponibilidad para esa fecha y esas personas. Prueba otro día.';
export const TEXTO_FRANJA_SIN_DISPONIBILIDAD = 'Sin disponibilidad (llena o ya terminó)';
export const TEXTO_FRANJA_SOLO_PERSONAL = 'Disponible solo para el personal';
export const AVISO_FRANJA_SOLO_PERSONAL =
  'La franja de hoy está disponible solo para el personal: el cliente web ya no puede reservarla porque '
  + 'faltan menos de 2 horas o ya empezó. Puedes registrarla mientras no haya terminado.';
export const TEXTO_FRANJA_SIN_CONFIRMAR = 'Disponibilidad sin confirmar';
export const TEXTO_FRANJA_DISPONIBLE = 'Disponible';
export const TEXTO_ACTUALIZAR_DISPONIBILIDAD = 'Actualizar disponibilidad';

export function textoFranjasDisponibles(cantidad) {
  return cantidad === 1 ? '1 franja con disponibilidad' : `${cantidad} franjas con disponibilidad`;
}

// Reserva creada
export const TITULO_RESERVA_CREADA = 'Reserva creada';
export const TEXTO_VER_EN_AGENDA = 'Ver en la agenda';
export const TEXTO_CREAR_OTRA = 'Crear otra';
export const TEXTO_MESA_ASIGNADA_CONSULTA = 'Mesa asignada: consulta la agenda.';
export const TEXTO_BUSCANDO_MESA = 'Buscando la mesa asignada…';
export const ETIQUETA_NUEVA = 'Nueva';
export const TEXTO_RESERVA_RECIEN_CREADA = 'Reserva recién creada';

// Descartar borrador
export const TITULO_DESCARTAR = '¿Descartar lo escrito?';
export const CUERPO_DESCARTAR = 'Si cierras ahora, se pierde lo que cambiaste.';
export const TEXTO_SEGUIR_EDITANDO = 'Seguir editando';
export const TEXTO_DESCARTAR = 'Descartar';

// Modificar
export const TEXTO_ACCION_MODIFICAR = 'Modificar';
export const TITULO_MODIFICAR = 'Modificar reserva';
export const TEXTO_REVISAR_CAMBIOS = 'Revisar cambios';
export const TEXTO_SIN_CAMBIOS = 'Todavía no hay cambios.';
export const TEXTO_CONFIRMAR_MODIFICAR = 'Sí, modificar reserva';
export const TEXTO_MODIFICANDO = 'Modificando…';
export const TEXTO_VOLVER_A_EDITAR = 'Volver a editar';
export const TITULO_CONFIRMAR_MODIFICAR = '¿Modificar esta reserva?';
export const CUERPO_CONFIRMAR_MODIFICAR = 'Se guardan los cambios marcados. El servidor valida la disponibilidad otra vez al guardar.';
export const EXITO_MODIFICAR = 'Reserva modificada.';
export const AYUDA_NUEVO_HORARIO = 'Un horario nuevo se valida como reserva nueva: hasta 30 días adelante y con la franja sin terminar.';
export const ETIQUETA_MESA = 'Mesa';
export const OPCION_MESA_AUTOMATICA = 'Automática (conserva la actual si sirve)';
export const TEXTO_MESA_ACTUAL = 'actual';
export const TEXTO_CARGANDO_MESAS = 'Consultando mesas libres…';
export const TEXTO_ACTUALIZAR_MESAS = 'Actualizar mesas';
export const TEXTO_CAMBIA = 'cambia';
export const TEXTO_MESAS_NO_CONFIRMADAS = 'No pudimos consultar las mesas libres. Puedes revisar los cambios y el servidor validará la mesa.';

export function textoSinMesasLibres(personas) {
  return `No hay mesas libres para ${personas} ${personas === 1 ? 'persona' : 'personas'} en esa fecha y franja.`;
}

export function textoMesasLibres(cantidad) {
  return cantidad === 1 ? '1 mesa disponible' : `${cantidad} mesas disponibles`;
}

// --- Mapa de mesas y visitas sin reserva (Lote 4, GI-396) ---

export const ETIQUETA_NAVEGACION_RESERVAS = 'Secciones de Reservas';
export const TEXTO_PESTANA_AGENDA = 'Agenda';
export const TEXTO_PESTANA_MAPA = 'Mapa';
export const TEXTO_PESTANA_VISITAS = 'Visitas';
export const TEXTO_PESTANA_CONFIGURACION = 'Configuración';

export const TEXTO_BOTON_VISITA_SIN_RESERVA = 'Visita sin reserva';

// Mapa
export const TEXTO_ACTUALIZAR_CADA_60 = 'Actualizar cada 60 s';
export const AYUDA_ACTUALIZAR_CADA_60 = 'Solo con esta pestaña visible; se pausa mientras hay un panel abierto.';
export const TEXTO_ACTUALIZACION_DETENIDA = 'La actualización automática se detuvo por un problema de sesión o de permisos.';
export const TEXTO_SIN_FRANJAS_MAPA = 'No hay franjas configuradas.';
export const TEXTO_SIN_ZONAS = 'No hay zonas ni mesas configuradas.';
export const TEXTO_ZONA_SIN_MESAS = 'Esta zona no tiene mesas.';
export const TEXTO_ZONA_INACTIVA = 'Zona inactiva';
export const TEXTO_VISITA_NO_DISPONIBLE = 'Para registrar visitas, elige hoy y una franja que ya empezó.';
export const TEXTO_ELIGE_FRANJA_MAPA = 'Elige una franja para ver el mapa.';
export const TEXTO_CARGANDO_MAPA = 'Cargando el mapa…';
export const TEXTO_ACCION_REGISTRAR_VISITA = 'Registrar visita';
export const TEXTO_ACCION_ABRIR_RESERVA = 'Abrir reserva';

export const ESTADOS_MESA = {
  Libre: { etiqueta: 'Libre', simbolo: '○', clase: 'libre' },
  Reservada: { etiqueta: 'Reservada', simbolo: '◐', clase: 'reservada' },
  Ocupada: { etiqueta: 'Ocupada', simbolo: '●', clase: 'ocupada' },
  Inactiva: { etiqueta: 'Inactiva', simbolo: '✕', clase: 'inactiva' },
};

export function textoConflictoMesaInactiva(cliente) {
  return cliente
    ? `Mesa inactiva con reserva vigente: ${cliente}`
    : 'Mesa inactiva con reserva vigente';
}

export function textoZonaResumen(libres, total) {
  return `${libres} ${libres === 1 ? 'libre' : 'libres'} de ${total} ${total === 1 ? 'mesa' : 'mesas'}`;
}

export function textoResumenMapa({ franja, horas, fechaLarga, resumen }) {
  const { libres, reservadas, ocupadas, inactivas } = resumen;
  return `${franja} ${horas}, ${fechaLarga}: ${libres} ${libres === 1 ? 'libre' : 'libres'}, `
    + `${reservadas} ${reservadas === 1 ? 'reservada' : 'reservadas'}, `
    + `${ocupadas} ${ocupadas === 1 ? 'ocupada' : 'ocupadas'}, `
    + `${inactivas} ${inactivas === 1 ? 'inactiva' : 'inactivas'}.`;
}

// Visitas sin reserva
export const TITULO_VISITA = 'Registrar visita';
export const TEXTO_FRANJA_EN_CURSO = 'En curso';
export const TEXTO_FRANJA_TERMINADA = 'Ya terminó (el servidor lo permite)';
export const TEXTO_SIN_FRANJAS_VISITA = 'No hay franjas activas configuradas.';
export const ETIQUETA_PERSONAS_VISITA = 'Personas';
export const ETIQUETA_MESA_VISITA = 'Mesa (opcional)';
export const OPCION_SIN_MESA = 'Sin mesa';
export const AYUDA_MESA_VISITA = 'La lista no incluye las mesas con una visita ya sentada: el sistema no las marca.';
export const AVISO_VISITA_DEFINITIVA = 'Estas visitas no se pueden editar ni borrar después.';
export const TEXTO_REGISTRAR_VISITA = 'Registrar visita';
export const TEXTO_REGISTRANDO_VISITA = 'Registrando…';
export const TEXTO_CONFIRMAR_VISITA = 'Sí, registrar visita';
export const TITULO_VISITA_REGISTRADA = 'Visita registrada';
export const TEXTO_REGISTRAR_OTRA = 'Registrar otra';
export const TEXTO_VER_MAPA = 'Ver el mapa';
export const AVISO_MESA_VISITA_SIN_MARCA = 'El mapa no marca como ocupadas las mesas con visitas sin reserva: la mesa seguirá viéndose Libre.';
export const TITULO_SESION_VISITAS = 'Registradas en esta sesión';
export const AYUDA_SESION_VISITAS = 'Solo se guardan en esta pantalla: se pierden al recargar la página.';

export function textoSinFranjaEmpezada(horaInicio) {
  return `Todavía no empezó ninguna franja de hoy. La primera empieza a las ${horaInicio}.`;
}

export function textoPersonas(cantidad) {
  return `${cantidad} ${cantidad === 1 ? 'persona' : 'personas'}`;
}

// --- Administración de zonas, mesas y franjas (Lote 5, GI-397) ---

export const ETIQUETA_SECCIONES_CONFIGURACION = 'Secciones de configuración';
export const TEXTO_PESTANA_ZONAS = 'Zonas';
export const TEXTO_PESTANA_MESAS = 'Mesas';
export const TEXTO_PESTANA_FRANJAS = 'Franjas';

export const RECURSOS_SALON = {
  zonas: { singular: 'zona', plural: 'zonas', nueva: 'Nueva zona', creada: 'Zona creada.', actualizada: 'Zona actualizada.' },
  mesas: { singular: 'mesa', plural: 'mesas', nueva: 'Nueva mesa', creada: 'Mesa creada.', actualizada: 'Mesa actualizada.' },
  franjas: { singular: 'franja', plural: 'franjas', nueva: 'Nueva franja', creada: 'Franja creada.', actualizada: 'Franja actualizada.' },
};

export const FILTROS_ESTADO_SALON = [
  ['todas', 'Todas'],
  ['activas', 'Activas'],
  ['inactivas', 'Inactivas'],
];
export const TEXTO_FILTRO_ZONA_TODAS = 'Todas las zonas';

export const TEXTO_ACTIVA = 'Activa';
export const TEXTO_INACTIVA = 'Inactiva';
export const TEXTO_ZONA_INACTIVA_ETIQUETA = 'Zona inactiva';
export const TEXTO_EDITAR = 'Editar';
export const TEXTO_DESACTIVAR = 'Desactivar';
export const TEXTO_REACTIVAR = 'Reactivar';
export const TEXTO_GUARDAR = 'Guardar';
export const TEXTO_GUARDANDO = 'Guardando…';
export const TEXTO_CANCELAR = 'Cancelar';

export const AYUDA_REASIGNAR = 'Reasígnalas o cancélalas desde la Agenda.';
export const AVISO_ZONAS_TRUNCADAS = 'Hay más de 100 zonas: los selectores muestran solo las primeras 100.';
export const TEXTO_ZONAS_NO_CARGARON = 'No pudimos cargar las zonas.';

export const ETIQUETA_NOMBRE = 'Nombre';
export const ETIQUETA_NUMERO_MESA = 'Número';
export const ETIQUETA_CAPACIDAD = 'Capacidad (personas)';
export const ETIQUETA_ZONA = 'Zona';
export const ETIQUETA_HORA_INICIO = 'Hora de inicio';
export const ETIQUETA_HORA_FIN = 'Hora de fin';
export const TEXTO_ZONA_ACTUAL = 'actual';
export const AYUDA_ZONA_MESA = 'Se ofrecen las zonas activas y, al editar, la zona actual de la mesa.';

export function textoSinElementos(plural, conFiltros) {
  return conFiltros ? `No hay ${plural} con esos filtros.` : `Todavía no hay ${plural} configuradas.`;
}

export function textoConteoSalon(total, singular, plural) {
  return `${total} ${total === 1 ? singular : plural}`;
}

// Cambio de estado: la consecuencia explica lo que el backend realmente hace.
export const CONSECUENCIA_DESACTIVAR = {
  zona: 'Sus mesas dejarán de ofrecerse para reservas nuevas y se verán Inactivas en el mapa. Si sus mesas tienen reservas futuras vigentes, el servidor no lo permite.',
  mesa: 'La mesa dejará de ofrecerse para reservas nuevas y se verá Inactiva en el mapa. Si tiene reservas futuras vigentes, el servidor no lo permite.',
  franja: 'La franja dejará de ofrecerse para reservas nuevas y visitas. Si tiene reservas futuras vigentes, el servidor no lo permite.',
};
export const CONSECUENCIA_REACTIVAR = {
  zona: 'Sus mesas vuelven a ofrecerse para reservas (las que sigan activas).',
  mesa: 'La mesa vuelve a ofrecerse para reservas nuevas. Si su zona está inactiva, seguirá sin ofrecerse hasta reactivar la zona.',
  franja: 'La franja vuelve a ofrecerse para reservas y visitas. El servidor valida que su horario no se solape con el de otra franja activa.',
};
export const TEXTO_CARGANDO_IMPACTO = 'Consultando las mesas de la zona…';
export const TEXTO_IMPACTO_NO_CARGO = 'No pudimos consultar las mesas de la zona; el servidor igual valida el cambio.';
export const TEXTO_CONFIRMAR_DESACTIVAR = 'Sí, desactivar';
export const TEXTO_CONFIRMAR_REACTIVAR = 'Sí, reactivar';
export const TEXTO_PROCESANDO = 'Procesando…';

export function tituloCambioEstado(activar, singular, nombre) {
  return `${activar ? 'Reactivar' : 'Desactivar'} ${singular} ${nombre}`;
}

// Zona, mesa y franja son femeninas: «Mesa desactivada.», «Franja reactivada.»
export function textoEstadoCambiado(singular, activa) {
  return `${singular.charAt(0).toUpperCase()}${singular.slice(1)} ${activa ? 'reactivada' : 'desactivada'}.`;
}

export function textoConfirmarVisita(cantidad, mesa) {
  return `¿Registrar visita de ${textoPersonas(cantidad)} en ${mesa ? `Mesa ${mesa}` : 'sin mesa'}? No se podrá editar ni borrar.`;
}
