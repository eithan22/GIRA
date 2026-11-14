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

// Campo único «Código o teléfono» de la agenda y búsqueda por código (GI-401).
export const ETIQUETA_BUSQUEDA = 'Código o teléfono';
export const AYUDA_BUSQUEDA = 'Escribe el código de 8 dígitos para ver la reserva al instante, o el teléfono del cliente (10 dígitos).';
export const MENSAJE_BUSQUEDA_INCOMPLETA = 'Sigue escribiendo: el código tiene 8 dígitos y el teléfono, 10.';
export const MENSAJE_BUSQUEDA_INVALIDA = 'Usa solo números: el código tiene 8 dígitos y el teléfono, 10.';
export const TITULO_RESERVA_ENCONTRADA = 'Reserva encontrada';
export const TEXTO_BUSCANDO_CODIGO = 'Buscando la reserva…';
export const TEXTO_RESERVA_ENCONTRADA_ANUNCIO = 'Reserva encontrada.';
// Al teclear un teléfono se pasa por 8 dígitos y se dispara una búsqueda por código: el texto lo explica.
export const TEXTO_CODIGO_NO_ENCONTRADO = 'No hay ninguna reserva con ese código. Si estás escribiendo un teléfono, sigue hasta los 10 dígitos.';
export const TEXTO_ERROR_CODIGO = 'No pudimos buscar la reserva. Intenta de nuevo.';
// Texto propio para el 429: nunca se muestra el mensaje neutro del servidor, y el campo sigue editable.
export const TEXTO_BUSQUEDA_LIMITE = 'Hiciste muchas búsquedas seguidas. Espera un momento e inténtalo de nuevo.';
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
export const TEXTO_ACCION_NO_ASISTIO = 'Marcar No asistió';
export const TEXTO_ACCION_CANCELAR = 'Cancelar reserva';

export const TEXTO_CONFIRMAR_CHECKIN = 'Confirmar check-in';
export const TEXTO_CONFIRMAR_COMPLETAR = 'Sí, completar visita';
export const TEXTO_CONFIRMAR_NO_ASISTIO = 'Sí, marcar No asistió';
export const TEXTO_CONFIRMAR_CANCELAR = 'Sí, cancelar reserva';
export const TEXTO_VOLVER = 'Volver';
export const TEXTO_MANTENER_RESERVA = 'Mantener reserva';

export const TITULO_CHECKIN = '¿Cuántas personas llegaron?';
export const CUERPO_CHECKIN = 'Se registra la llegada y la reserva pasa a En curso.';
export const TITULO_COMPLETAR = '¿Completar la visita?';
export const CUERPO_COMPLETAR = 'Se registra la visita como completada y la mesa queda libre.';
export const TITULO_NO_ASISTIO = '¿Marcar como no asistida?';
export const CUERPO_NO_ASISTIO = 'El cliente no llegó: la reserva pasa a No asistió y la mesa queda libre. Esta acción no se puede deshacer. Si el cliente llega después, regístralo como visita sin reserva.';
export const TITULO_CANCELAR = '¿Cancelar esta reserva?';
export const CUERPO_CANCELAR = 'La mesa quedará libre y esta acción no se puede deshacer. El motivo queda guardado en la reserva.';

export const EXITO_CHECKIN = 'Check-in registrado. La reserva está En curso.';
export const EXITO_COMPLETAR = 'Visita completada.';
export const EXITO_CANCELAR = 'Reserva cancelada.';
export const EXITO_NO_ASISTIO = 'Reserva marcada como No asistió. La mesa quedó libre.';

// Solo informa lo que pasó; no promete ningún flujo posterior.
export const AVISO_NO_ASISTIO = 'El cliente no llegó, por eso la reserva quedó como no asistida.';

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
export const mensajeFechaFueraDeRango = (dias) => `Elige una fecha desde hoy hasta ${dias} ${dias === 1 ? 'día' : 'días'} adelante.`;
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
export const avisoFranjaSoloPersonal = (antelacionTexto) =>
  'La franja de hoy está disponible solo para el personal: el cliente web ya no puede reservarla porque '
  + `faltan menos de ${antelacionTexto} o ya empezó. Puedes registrarla mientras no haya terminado.`;
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
export const ayudaNuevoHorario = (dias) =>
  `Un horario nuevo se valida como reserva nueva: hasta ${dias} ${dias === 1 ? 'día' : 'días'} adelante y con la franja sin terminar.`;
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
export const TEXTO_PESTANA_CONFIGURACION = 'Configuración';

export const TEXTO_BOTON_VISITA_SIN_RESERVA = 'Visita sin reserva';

// Sección «Visitas sin reserva de hoy» de la Agenda (GI-402).
export const TITULO_VISITAS_HOY = 'Visitas sin reserva de hoy';
export const TEXTO_VISITAS_VACIO = 'Todavía no hay visitas sin reserva hoy.';
export const TEXTO_VISITAS_CARGANDO = 'Cargando las visitas…';
export const TEXTO_VISITAS_ERROR = 'No pudimos cargar las visitas de hoy.';
export const AYUDA_VISITAS_SIN_MARCA = 'Estas visitas no marcan la mesa como ocupada en el mapa: no tienen hora de salida.';
// Una visita sin registro de hora (no debería pasar en producción): texto neutro en vez de una hora inventada.
export const TEXTO_VISITA_SIN_HORA = 'sin hora';

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
export const TEXTO_CARGANDO_FRANJAS = 'Cargando las franjas…';
export const TEXTO_ERROR_FRANJAS = 'No pudimos cargar las franjas.';
export const ETIQUETA_PERSONAS_VISITA = 'Personas';
export const ETIQUETA_MESA_VISITA = 'Mesa (opcional)';
export const OPCION_SIN_MESA = 'Sin mesa';
export const AYUDA_MESA_VISITA = 'La lista puede incluir mesas con una visita ya sentada: el sistema no las marca como ocupadas.';
export const AVISO_VISITA_DEFINITIVA = 'Estas visitas no se pueden editar ni borrar después.';
export const TEXTO_REGISTRAR_VISITA = 'Registrar visita';
export const TEXTO_REGISTRANDO_VISITA = 'Registrando…';
export const TEXTO_CONFIRMAR_VISITA = 'Sí, registrar visita';
export const TITULO_VISITA_REGISTRADA = 'Visita registrada';
export const TEXTO_REGISTRAR_OTRA = 'Registrar otra';
export const AVISO_MESA_VISITA_SIN_MARCA = 'El mapa no marca como ocupadas las mesas con visitas sin reserva: la mesa seguirá viéndose Libre.';

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

// --- Configuración de reservas: Políticas, Horario y Días cerrados (GI-226, Lote 6) ---

export const TEXTO_PESTANA_POLITICAS = 'Políticas';
export const TEXTO_PESTANA_HORARIO = 'Horario';
export const TEXTO_PESTANA_DIAS_CERRADOS = 'Días cerrados';

export const TITULO_CAMBIOS_SIN_GUARDAR = '¿Salir sin guardar?';
export const CUERPO_CAMBIOS_SIN_GUARDAR = 'Hay cambios sin guardar en esta pestaña. Si cambias de pestaña, se pierden.';
export const TEXTO_DESCARTAR_Y_CAMBIAR = 'Descartar y cambiar';

export const INTRO_POLITICAS =
  'Las reglas de creación (antelación, reservas por teléfono y tamaño de grupo) rigen para las reservas nuevas y no '
  + 'invalidan las existentes. La ventana de check-in y el límite de cancelación se leen en el momento de usarlos.';
export const TEXTO_RESTABLECER = 'Restablecer valores recomendados';
export const TEXTO_DESCARTAR_CAMBIOS = 'Descartar cambios';
export const AVISO_RECOMENDADOS_CARGADOS = 'Valores recomendados cargados; pulsa Guardar';
export const EXITO_POLITICAS = 'Políticas guardadas.';
export const TEXTO_GUARDAR_POLITICAS = 'Guardar políticas';
export const textoUltimaActualizacion = (fecha) => `Última actualización: ${fecha}`;
export const textoPorDefecto = (valor, unidad) => `Por defecto: ${valor} ${unidad}.`;
export const ERROR_CORREGIR_CAMPOS = 'Corrige los campos marcados antes de guardar.';
export const TEXTO_CARGANDO_CONFIGURACION = 'Cargando la configuración…';
export const TEXTO_CONFIGURACION_NO_CARGO = 'No pudimos cargar la configuración.';

/** Etiqueta y ayuda de cada política: qué hace y a qué reservas aplica. El valor por defecto lo pone la pantalla. */
export const TEXTOS_CAMPOS_POLITICAS = {
  antelacionMinimaMinutos: {
    etiqueta: 'Antelación mínima',
    ayuda: 'Con cuánto tiempo de anticipación se puede reservar por la web. Aplica a las reservas nuevas.',
  },
  antelacionMaximaDias: {
    etiqueta: 'Antelación máxima',
    ayuda: 'Hasta cuántos días adelante se puede reservar (de 1 a 365). Aplica a las reservas nuevas.',
  },
  limiteCancelacionMinutos: {
    etiqueta: 'Límite de cancelación',
    ayuda: 'Hasta cuánto antes del inicio de la franja puede cancelar el cliente. Se evalúa al cancelar, también en reservas ya hechas.',
  },
  ventanaCheckInMinutos: {
    etiqueta: 'Ventana de check-in',
    ayuda: 'Desde cuánto antes del inicio de la franja se permite el check-in (de 0 a 120). Se evalúa en el momento del check-in.',
  },
  maxReservasVigentesPorTelefono: {
    etiqueta: 'Reservas vigentes por teléfono',
    ayuda: 'Máximo de reservas Confirmadas o En curso con un mismo teléfono (de 1 a 10). Aplica a las reservas nuevas.',
  },
  maxPersonasPorReserva: {
    etiqueta: 'Tamaño máximo de grupo',
    ayuda: 'Máximo de personas por reserva (de 1 a 50). Nunca supera la mesa activa más grande. Aplica a las reservas nuevas.',
  },
};

export const INTRO_HORARIO =
  'Horario de operación de cada día. Las franjas activas deben caber en el horario de todos los días abiertos. '
  + 'Reducir el horario o cerrar un día no cancela las reservas ya hechas.';
export const ETIQUETA_ABIERTO = 'Abierto';
export const ETIQUETA_APERTURA = 'Apertura';
export const ETIQUETA_CIERRE = 'Cierre';
export const TEXTO_DIA_CERRADO = 'Cerrado';
export const exitoHorario = (dia) => `Horario del ${dia.toLowerCase()} guardado.`;

export const INTRO_DIAS_CERRADOS =
  'Fechas concretas en las que el restaurante no abre (feriados, eventos). No se cancelan las reservas ya hechas: '
  + 'solo se impiden las nuevas ese día.';
export const ETIQUETA_FECHA_CIERRE = 'Fecha';
export const ETIQUETA_MOTIVO_CIERRE = 'Motivo (opcional)';
export const TEXTO_CERRAR_DIA = 'Cerrar este día';
export const TEXTO_QUITAR_CIERRE = 'Quitar';
export const TEXTO_CONFIRMAR_QUITAR = 'Sí, quitar';
export const textoPreguntaQuitar = (fecha) => `¿Quitar el cierre del ${fecha}?`;
export const EXITO_DIA_CERRADO = 'Día cerrado agregado.';
export const EXITO_CIERRE_QUITADO = 'Cierre quitado.';
export const TEXTO_SIN_DIAS_CERRADOS = 'No hay días cerrados próximos.';
