// Pruebas de funciones puras. Se corren con el ejecutor de Node, sin instalar nada:
//   node --test src/reservas/personal/reservasPersonalFormato.test.js
// No se importa desde ningún componente, así que Vite no las empaqueta.
import test from 'node:test';
import assert from 'node:assert/strict';
import {
  ESTADOS_FILTRO,
  ORIGEN_API,
  PERSONAS_POR_DEFECTO,
  accionMesa,
  activaDeFiltro,
  agruparMesasPorZona,
  armarCuerpoCrearReserva,
  armarCuerpoFranja,
  armarCuerpoMesa,
  armarCuerpoVisita,
  armarCuerpoZona,
  contarMesasZona,
  esConflictoReasignar,
  listaIncompleta,
  mesaConZonaInactiva,
  normalizarHora,
  reemplazarPorId,
  textoImpactoZona,
  validarCapacidadMesa,
  validarHorarioFranja,
  validarNombreFranja,
  validarNombreZona,
  validarNumeroMesa,
  validarZonaMesa,
  zonasParaMesa,
  armarFiltrosAgenda,
  armarModificacion,
  contextoSantoDomingo,
  detalleAFila,
  esBorradorCrear,
  esConflictoMesaInactiva,
  esFechaIso,
  estadoFranja,
  esFranjaSoloPersonal,
  etiquetaOrigen,
  fechaEnRango,
  filaDesdeMesa,
  formatearFechaHoraSantoDomingo,
  formatearTelefono,
  franjaDefectoVisita,
  franjaPorDefecto,
  franjasConEstado,
  franjasParaVisita,
  hayCambios,
  horaActualSantoDomingo,
  hoyEnSantoDomingo,
  inicioPrimeraFranja,
  mesasSeleccionables,
  minutosDeHora,
  origenAApi,
  paginasTotales,
  puedeRegistrarVisita,
  rangoFechasReserva,
  resumenCambios,
  resumenGlobal,
  resumenPorZona,
  soloDigitos,
  sumarDias,
  validarMotivo,
  validarNombreCliente,
  validarPersonas,
  validarPersonasReales,
  validarPersonasVisita,
  validarTelefono,
} from './reservasPersonalFormato.js';

test('hoyEnSantoDomingo usa UTC-4: 02:30 UTC del 12 es todavía el 11 en Santo Domingo', () => {
  assert.equal(hoyEnSantoDomingo(new Date('2026-11-12T02:30:00Z')), '2026-11-11');
  assert.equal(hoyEnSantoDomingo(new Date('2026-11-12T04:00:00Z')), '2026-11-12');
});

test('horaActualSantoDomingo devuelve HH:mm de 24 horas', () => {
  assert.equal(horaActualSantoDomingo(new Date('2026-11-11T20:05:00Z')), '16:05');
  assert.equal(horaActualSantoDomingo(new Date('2026-11-12T04:00:00Z')), '00:00');
});

test('sumarDias cruza mes, año y año bisiesto sin desfases', () => {
  assert.equal(sumarDias('2026-11-30', 1), '2026-12-01');
  assert.equal(sumarDias('2026-12-31', 1), '2027-01-01');
  assert.equal(sumarDias('2026-03-01', -1), '2026-02-28');
  assert.equal(sumarDias('2028-02-28', 1), '2028-02-29');
  assert.equal(sumarDias('2026-11-11', 0), '2026-11-11');
});

test('esFechaIso exige aaaa-mm-dd y una fecha real', () => {
  assert.equal(esFechaIso('2026-11-11'), true);
  assert.equal(esFechaIso('2026-02-30'), false);
  assert.equal(esFechaIso('11/11/2026'), false);
  assert.equal(esFechaIso(''), false);
  assert.equal(esFechaIso(undefined), false);
});

test('soloDigitos quita espacios, guiones y paréntesis', () => {
  assert.equal(soloDigitos('(809) 555-0199'), '8095550199');
  assert.equal(soloDigitos(null), '');
});

test('validarTelefono acepta 10 dígitos con 809, 829 o 849 aunque traiga separadores', () => {
  for (const telefono of ['8095550199', '829 555 0199', '849-555-0199', '(809) 555 0199']) {
    const r = validarTelefono(telefono);
    assert.equal(r.valido, true, telefono);
    assert.equal(r.digitos.length, 10);
  }
});

test('validarTelefono rechaza longitud y prefijos inválidos con el texto del backend', () => {
  for (const telefono of ['809555019', '80955501999', '7015550199', '', 'abc']) {
    const r = validarTelefono(telefono);
    assert.equal(r.valido, false, telefono);
    assert.equal(r.mensaje, 'El teléfono debe tener 10 dígitos y empezar con 809, 829 o 849.');
  }
});

test('validarTelefono detecta el código de país (11 dígitos con 1) y da el texto propio', () => {
  const r = validarTelefono('+1 809 555 0199');
  assert.equal(r.valido, false);
  assert.equal(r.mensaje, 'Escribe solo los 10 dígitos de tu teléfono (809, 829 o 849), sin código de país.');
});

test('formatearTelefono agrupa 3-3-4 y no toca lo que no tiene 10 dígitos', () => {
  assert.equal(formatearTelefono('8095550199'), '809 555 0199');
  assert.equal(formatearTelefono('809555'), '809555');
  assert.equal(formatearTelefono(undefined), '');
});

test('etiquetaOrigen muestra "Por llamada" y "En persona" y deja Web y lo desconocido', () => {
  assert.equal(etiquetaOrigen('Telefono'), 'Por llamada');
  assert.equal(etiquetaOrigen('Presencial'), 'En persona');
  assert.equal(etiquetaOrigen('Web'), 'Web');
  assert.equal(etiquetaOrigen('Otro'), 'Otro');
  assert.equal(etiquetaOrigen(undefined), '');
});

const ALMUERZO_HOY = { horaInicio: '12:00:00', horaFin: '15:00:00' };
const soloPersonal = (hora, extra = {}) =>
  esFranjaSoloPersonal({ fecha: '2026-12-01', hoy: '2026-12-01', hora, franja: ALMUERZO_HOY, ...extra });

test('esFranjaSoloPersonal: a 2 h exactas del inicio todavía puede reservar la web; un minuto después, solo el personal', () => {
  assert.equal(soloPersonal('10:00'), false);
  assert.equal(soloPersonal('10:01'), true);
});

test('esFranjaSoloPersonal: en curso es solo del personal hasta un minuto antes del fin', () => {
  assert.equal(soloPersonal('12:00'), true);
  assert.equal(soloPersonal('13:30'), true);
  assert.equal(soloPersonal('14:59'), true);
});

test('esFranjaSoloPersonal: desde el fin exacto la franja terminó y ya no cuenta', () => {
  assert.equal(soloPersonal('15:00'), false);
  assert.equal(soloPersonal('16:00'), false);
});

test('esFranjaSoloPersonal: otra fecha o sin franja no aplica', () => {
  assert.equal(soloPersonal('13:00', { fecha: '2026-12-02' }), false);
  assert.equal(soloPersonal('13:00', { franja: undefined }), false);
  assert.equal(soloPersonal('08:00'), false);
});

test('ESTADOS_FILTRO son exactamente los nombres del enum del backend', () => {
  assert.deepEqual(ESTADOS_FILTRO, ['Confirmada', 'EnCurso', 'Completada', 'Cancelada', 'NoAsistio']);
});

test('armarFiltrosAgenda: estado exacto del enum, y "En curso" con espacio se descarta', () => {
  assert.equal(armarFiltrosAgenda({ fecha: '2026-11-11', estado: 'EnCurso', page: 1 }).estado, 'EnCurso');
  assert.equal(armarFiltrosAgenda({ fecha: '2026-11-11', estado: 'NoAsistio', page: 1 }).estado, 'NoAsistio');
  assert.equal('estado' in armarFiltrosAgenda({ fecha: '2026-11-11', estado: 'En curso', page: 1 }), false);
  assert.equal('estado' in armarFiltrosAgenda({ fecha: '2026-11-11', estado: '', page: 1 }), false);
});

test('armarFiltrosAgenda: con teléfono manda solo dígitos y no manda fecha', () => {
  const f = armarFiltrosAgenda({ fecha: '2026-11-11', telefono: '809 555-0199', page: 2 });
  assert.equal(f.telefono, '8095550199');
  assert.equal('fecha' in f, false);
  assert.equal(f.page, 2);
  assert.equal(f.pageSize, 20);
});

test('armarFiltrosAgenda: sin teléfono manda la fecha y la franja', () => {
  const f = armarFiltrosAgenda({ fecha: '2026-11-11', franjaId: 'abc', telefono: '', page: 0 });
  assert.equal(f.fecha, '2026-11-11');
  assert.equal(f.franjaId, 'abc');
  assert.equal('telefono' in f, false);
  assert.equal(f.page, 1);
});

test('paginasTotales redondea hacia arriba y nunca baja de 1', () => {
  assert.equal(paginasTotales(0), 1);
  assert.equal(paginasTotales(20), 1);
  assert.equal(paginasTotales(21), 2);
  assert.equal(paginasTotales(100), 5);
});

test('detalleAFila conserva los campos de la fila y descarta los extra del detalle', () => {
  const fila = detalleAFila({
    id: '1', codigo: '48271936', estado: 'EnCurso', fecha: '2026-11-11', franjaId: 'f',
    franjaNombre: 'Almuerzo', franjaHoraInicio: '12:00:00', franjaHoraFin: '15:00:00',
    mesaId: 'm', mesaNumero: 4, mesaCapacidad: 6, zonaNombre: 'Patio', personas: 4, personasReales: 3,
    fechaCheckIn: 'x', clienteId: 'c', clienteNombre: 'Ana', clienteTelefono: '8095550199',
    origen: 'Web', motivoCancelacion: null, creadaEn: 'y',
  });
  assert.equal(fila.estado, 'EnCurso');
  assert.equal(fila.personasReales, 3);
  assert.equal('mesaCapacidad' in fila, false);
  assert.equal('motivoCancelacion' in fila, false);
});

test('formatearFechaHoraSantoDomingo: con Z convierte a UTC-4', () => {
  assert.equal(formatearFechaHoraSantoDomingo('2026-11-11T13:12:00Z'), '11/11/2026 09:12');
});

test('formatearFechaHoraSantoDomingo: sin Z se toma como UTC (no como hora local del navegador)', () => {
  assert.equal(formatearFechaHoraSantoDomingo('2026-11-11T13:12:00'), '11/11/2026 09:12');
  assert.equal(formatearFechaHoraSantoDomingo('2026-11-11T13:12:00.1234567'), '11/11/2026 09:12');
});

test('formatearFechaHoraSantoDomingo: vacío o inválido da cadena vacía', () => {
  assert.equal(formatearFechaHoraSantoDomingo(null), '');
  assert.equal(formatearFechaHoraSantoDomingo('no-es-fecha'), '');
});

test('validarPersonasReales: entero entre 1 y la capacidad', () => {
  assert.equal(validarPersonasReales('3', 6).valido, true);
  assert.equal(validarPersonasReales(' 6 ', 6).valor, 6);
  assert.equal(validarPersonasReales('0', 6).mensaje, 'Debe haber al menos 1 persona.');
  assert.equal(validarPersonasReales('7', 6).mensaje, 'La mesa admite como máximo 6 personas.');
  assert.equal(validarPersonasReales('2.5', 6).valido, false);
  assert.equal(validarPersonasReales('', 6).valido, false);
});

test('validarMotivo: obligatorio, se recorta y admite hasta 500 caracteres', () => {
  assert.equal(validarMotivo('   ').valido, false);
  assert.equal(validarMotivo('  Cliente avisó  ').motivo, 'Cliente avisó');
  assert.equal(validarMotivo('a'.repeat(500)).valido, true);
  assert.equal(validarMotivo('a'.repeat(501)).valido, false);
  assert.equal(validarMotivo('a'.repeat(501)).mensaje, 'El motivo no puede superar 500 caracteres.');
});

// --- Lote 3 (GI-395) ---

test('ORIGEN_API: Telefono = 1 y Presencial = 2, y no incluye Web', () => {
  assert.deepEqual({ ...ORIGEN_API }, { Telefono: 1, Presencial: 2 });
  assert.equal('Web' in ORIGEN_API, false);
  assert.equal(Object.isFrozen(ORIGEN_API), true);
});

test('origenAApi devuelve número y rechaza Web, vacío, números y textos libres', () => {
  assert.equal(origenAApi('Telefono'), 1);
  assert.equal(origenAApi('Presencial'), 2);
  for (const invalido of ['Web', '', 'telefono', 1, 2, null, undefined, '__proto__', 'toString']) {
    assert.throws(() => origenAApi(invalido), /Telefono o Presencial/, String(invalido));
  }
});

test('armarCuerpoCrearReserva: origen numérico, teléfono solo dígitos, nombre recortado y personas número', () => {
  const cuerpo = armarCuerpoCrearReserva({
    nombre: '  Ana Pérez ', telefono: '(809) 555-0199', fecha: '2026-11-14',
    franjaHorariaId: 'abc', personas: '4', origen: 'Telefono',
  });
  assert.deepEqual(cuerpo, {
    nombre: 'Ana Pérez', telefono: '8095550199', fecha: '2026-11-14',
    franjaHorariaId: 'abc', personas: 4, origen: 1,
  });
  assert.equal(typeof cuerpo.origen, 'number');
  assert.equal(armarCuerpoCrearReserva({ ...cuerpo, origen: 'Presencial', personas: 2 }).origen, 2);
});

test('armarCuerpoCrearReserva nunca envía Web', () => {
  assert.throws(() => armarCuerpoCrearReserva({ nombre: 'A', telefono: '8095550199', fecha: '2026-11-14', franjaHorariaId: 'x', personas: 2, origen: 'Web' }));
});

test('validarNombreCliente: obligatorio, recortado y de hasta 150 caracteres', () => {
  assert.equal(validarNombreCliente('   ').valido, false);
  assert.equal(validarNombreCliente('   ').mensaje, 'El nombre es obligatorio.');
  assert.equal(validarNombreCliente('  Ana ').nombre, 'Ana');
  assert.equal(validarNombreCliente('a'.repeat(150)).valido, true);
  assert.equal(validarNombreCliente('a'.repeat(151)).mensaje, 'El nombre no puede superar 150 caracteres.');
});

test('validarPersonas: entero desde 1 y sin tope en el cliente', () => {
  assert.equal(validarPersonas('4').valor, 4);
  assert.equal(validarPersonas('500').valido, true);
  assert.equal(validarPersonas('0').mensaje, 'Debe haber al menos 1 persona.');
  assert.equal(validarPersonas('2.5').valido, false);
  assert.equal(validarPersonas('').valido, false);
  assert.equal(validarPersonas('-1').valido, false);
});

test('rangoFechasReserva y fechaEnRango: de hoy a hoy + 30 días', () => {
  assert.deepEqual(rangoFechasReserva('2026-11-11'), { min: '2026-11-11', max: '2026-12-11' });
  assert.equal(fechaEnRango('2026-11-11', '2026-11-11'), true);
  assert.equal(fechaEnRango('2026-12-11', '2026-11-11'), true);
  assert.equal(fechaEnRango('2026-12-12', '2026-11-11'), false);
  assert.equal(fechaEnRango('2026-11-10', '2026-11-11'), false);
  assert.equal(fechaEnRango('', '2026-11-11'), false);
});

const FRANJAS = [
  { id: 'a', nombre: 'Almuerzo', horaInicio: '12:00:00', horaFin: '15:00:00', activa: true },
  { id: 'b', nombre: 'Cena', horaInicio: '18:00:00', horaFin: '22:30:00', activa: true },
  { id: 'c', nombre: 'Vieja', horaInicio: '09:00:00', horaFin: '11:00:00', activa: false },
];

test('franjasConEstado: solo activas, y marca disponible true o false según la lista', () => {
  const r = franjasConEstado(FRANJAS, [{ id: 'a' }]);
  assert.deepEqual(r.map((f) => [f.id, f.disponible]), [['a', true], ['b', false]]);
});

test('franjasConEstado: sin lista de disponibles (por ejemplo tras un 429) quedan elegibles con null', () => {
  assert.deepEqual(franjasConEstado(FRANJAS, null).map((f) => f.disponible), [null, null]);
  assert.deepEqual(franjasConEstado(FRANJAS, undefined).map((f) => f.disponible), [null, null]);
  assert.deepEqual(franjasConEstado(FRANJAS, []).map((f) => f.disponible), [false, false]);
});

const MAPA = {
  zonas: [
    {
      nombre: 'Patio',
      mesas: [
        { mesaId: 'm4', numero: 4, capacidad: 6, estado: 'Reservada', reservaId: 'r1' },
        { mesaId: 'm5', numero: 5, capacidad: 2, estado: 'Libre', reservaId: null },
        { mesaId: 'm6', numero: 6, capacidad: 4, estado: 'Reservada', reservaId: 'r2' },
      ],
    },
    {
      nombre: 'Salón',
      mesas: [
        { mesaId: 'm7', numero: 7, capacidad: 6, estado: 'Libre', reservaId: null },
        { mesaId: 'm8', numero: 8, capacidad: 6, estado: 'Inactiva', reservaId: 'r1' },
        { mesaId: 'm9', numero: 9, capacidad: 8, estado: 'Ocupada', reservaId: 'r3' },
      ],
    },
  ],
};

test('mesasSeleccionables: libres con capacidad suficiente más la mesa actual de esta reserva', () => {
  const r = mesasSeleccionables(MAPA, 4, 'r1');
  assert.deepEqual(r.map((m) => [m.numero, m.actual]), [[4, true], [7, false]]);
});

test('mesasSeleccionables: no ofrece reservadas de otros, ocupadas ni inactivas, aunque tengan reservaId', () => {
  const numeros = mesasSeleccionables(MAPA, 1, 'r1').map((m) => m.numero);
  assert.deepEqual(numeros, [4, 5, 7]);
});

test('mesasSeleccionables: sin reserva actual solo devuelve libres, y con grupo grande puede quedar vacío', () => {
  assert.deepEqual(mesasSeleccionables(MAPA, 1).map((m) => m.numero), [5, 7]);
  assert.deepEqual(mesasSeleccionables(MAPA, 20, 'r1'), []);
  assert.deepEqual(mesasSeleccionables(null, 2), []);
});

const ANTES = {
  fecha: '2026-11-14', fechaTexto: 'Sáb 14 nov 2026', franjaId: 'a', franjaTexto: 'Almuerzo',
  personas: 4, mesaId: 'm4', mesaTexto: 'Mesa 4',
};

test('resumenCambios: sin tocar nada no hay cambios', () => {
  const filas = resumenCambios(ANTES, { ...ANTES, mesaId: '', mesaTexto: 'Automática' });
  assert.equal(hayCambios(filas), false);
});

test('resumenCambios: marca solo las filas que cambian', () => {
  const filas = resumenCambios(ANTES, { ...ANTES, fecha: '2026-11-15', fechaTexto: 'Dom 15 nov 2026', personas: '6', mesaId: '', mesaTexto: 'Automática' });
  assert.deepEqual(filas.filter((f) => f.cambia).map((f) => f.clave), ['fecha', 'personas']);
  assert.equal(hayCambios(filas), true);
});

test('resumenCambios: elegir la misma mesa no cambia; elegir otra sí; en automático no cambia', () => {
  assert.equal(resumenCambios(ANTES, { ...ANTES, mesaId: 'm4' }).find((f) => f.clave === 'mesa').cambia, false);
  assert.equal(resumenCambios(ANTES, { ...ANTES, mesaId: 'm7' }).find((f) => f.clave === 'mesa').cambia, true);
  assert.equal(resumenCambios(ANTES, { ...ANTES, mesaId: '' }).find((f) => f.clave === 'mesa').cambia, false);
});

test('armarModificacion: mesa vacía viaja como null y personas como número', () => {
  assert.deepEqual(
    armarModificacion({ fecha: '2026-11-14', franjaId: 'a', personas: '5', mesaId: '' }),
    { fecha: '2026-11-14', franjaHorariaId: 'a', personas: 5, mesaId: null },
  );
  assert.equal(armarModificacion({ fecha: 'x', franjaId: 'a', personas: 2, mesaId: 'm7' }).mesaId, 'm7');
});

test('esBorradorCrear: el formulario inicial no es borrador; cualquier cambio sí', () => {
  const inicial = { origen: '', nombre: '', telefono: '', fecha: '2026-11-11', personas: PERSONAS_POR_DEFECTO, franjaId: '' };
  assert.equal(esBorradorCrear(inicial, '2026-11-11'), false);
  assert.equal(esBorradorCrear({ ...inicial, origen: 'Telefono' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear({ ...inicial, nombre: ' Ana ' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear({ ...inicial, nombre: '   ' }, '2026-11-11'), false);
  assert.equal(esBorradorCrear({ ...inicial, telefono: '809' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear({ ...inicial, personas: '3' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear({ ...inicial, fecha: '2026-11-12' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear({ ...inicial, franjaId: 'a' }, '2026-11-11'), true);
  assert.equal(esBorradorCrear(inicial, '2026-11-11', { id: 'c' }), true);
});

// --- Lote 4 (GI-396) ---

const DIA = [
  { id: 'inactiva', nombre: 'Desayuno', horaInicio: '09:00:00', horaFin: '11:00:00', activa: false },
  { id: 'cena', nombre: 'Cena', horaInicio: '18:00:00', horaFin: '22:30:00', activa: true },
  { id: 'almuerzo', nombre: 'Almuerzo', horaInicio: '12:00:00', horaFin: '15:00:00', activa: true },
];
const HOY = '2026-11-11';
const por = (hora, fecha = HOY) => franjaPorDefecto(DIA, { fecha, hoy: HOY, hora })?.id;

test('minutosDeHora y estadoFranja respetan los límites: el inicio ya es en curso y el fin ya terminó', () => {
  assert.equal(minutosDeHora('12:30:00'), 750);
  assert.equal(minutosDeHora('08:05'), 485);
  const almuerzo = DIA[2];
  assert.equal(estadoFranja(almuerzo, '11:59'), 'noEmpezo');
  assert.equal(estadoFranja(almuerzo, '12:00'), 'enCurso');
  assert.equal(estadoFranja(almuerzo, '14:59'), 'enCurso');
  assert.equal(estadoFranja(almuerzo, '15:00'), 'terminada');
});

test('franjaPorDefecto, hoy: la franja en curso', () => {
  assert.equal(por('13:00'), 'almuerzo');
  assert.equal(por('12:00'), 'almuerzo');
  assert.equal(por('19:30'), 'cena');
});

test('franjaPorDefecto, hoy entre franjas o antes de abrir: la próxima que empieza', () => {
  assert.equal(por('15:00'), 'cena');
  assert.equal(por('16:30'), 'cena');
  assert.equal(por('08:00'), 'almuerzo');
});

test('franjaPorDefecto, hoy tras el cierre: la última del día', () => {
  assert.equal(por('22:30'), 'cena');
  assert.equal(por('23:45'), 'cena');
});

test('franjaPorDefecto, otro día: la primera franja activa sin importar la hora', () => {
  assert.equal(por('19:00', '2026-11-12'), 'almuerzo');
  assert.equal(por('08:00', '2026-11-10'), 'almuerzo');
});

test('franjaPorDefecto nunca elige una franja inactiva, aunque contenga la hora', () => {
  assert.equal(por('10:00'), 'almuerzo');
  assert.equal(franjaPorDefecto([DIA[0]], { fecha: HOY, hoy: HOY, hora: '10:00' }), null);
});

test('franjaPorDefecto sin franjas devuelve null', () => {
  assert.equal(franjaPorDefecto([], { fecha: HOY, hoy: HOY, hora: '10:00' }), null);
  assert.equal(franjaPorDefecto(undefined, { fecha: HOY, hoy: HOY, hora: '10:00' }), null);
});

test('hoy y franja se calculan en Santo Domingo y no con la zona del navegador', () => {
  // 01:30 UTC del 12 sigue siendo el 11 a las 21:30 en Santo Domingo: la Cena está en curso.
  const noche = contextoSantoDomingo(new Date('2026-11-12T01:30:00Z'));
  assert.deepEqual(noche, { hoy: '2026-11-11', hora: '21:30' });
  assert.equal(franjaPorDefecto(DIA, { fecha: noche.hoy, hoy: noche.hoy, hora: noche.hora }).id, 'cena');
  // 16:00 UTC son las 12:00 en Santo Domingo: justo empieza el almuerzo.
  const mediodia = contextoSantoDomingo(new Date('2026-11-11T16:00:00Z'));
  assert.equal(mediodia.hora, '12:00');
  assert.equal(franjaPorDefecto(DIA, { fecha: mediodia.hoy, hoy: mediodia.hoy, hora: mediodia.hora }).id, 'almuerzo');
  // 19:00 UTC son las 15:00: el almuerzo acaba de terminar y la próxima es la cena.
  const tarde = contextoSantoDomingo(new Date('2026-11-11T19:00:00Z'));
  assert.equal(franjaPorDefecto(DIA, { fecha: tarde.hoy, hoy: tarde.hoy, hora: tarde.hora }).id, 'cena');
});

test('franjasParaVisita: solo activas, ordenadas, con estado; las que no empezaron no son elegibles', () => {
  const r = franjasParaVisita(DIA, '16:00');
  assert.deepEqual(r.map((f) => [f.id, f.estado, f.elegible]), [['almuerzo', 'terminada', true], ['cena', 'noEmpezo', false]]);
});

test('franjaDefectoVisita: la actual; si no hay, la última ya empezada; si ninguna empezó, null', () => {
  assert.equal(franjaDefectoVisita(franjasParaVisita(DIA, '13:00')).id, 'almuerzo');
  assert.equal(franjaDefectoVisita(franjasParaVisita(DIA, '16:00')).id, 'almuerzo');
  assert.equal(franjaDefectoVisita(franjasParaVisita(DIA, '19:00')).id, 'cena');
  assert.equal(franjaDefectoVisita(franjasParaVisita(DIA, '08:00')), null);
});

test('inicioPrimeraFranja: el inicio de la primera activa o null', () => {
  assert.equal(inicioPrimeraFranja(DIA), '12:00:00');
  assert.equal(inicioPrimeraFranja([DIA[0]]), null);
});

test('puedeRegistrarVisita: solo hoy y con franja activa que ya empezó', () => {
  const almuerzo = DIA[2];
  assert.equal(puedeRegistrarVisita({ fecha: HOY, hoy: HOY, franja: almuerzo, hora: '13:00' }), true);
  assert.equal(puedeRegistrarVisita({ fecha: HOY, hoy: HOY, franja: almuerzo, hora: '16:00' }), true);
  assert.equal(puedeRegistrarVisita({ fecha: HOY, hoy: HOY, franja: almuerzo, hora: '11:00' }), false);
  assert.equal(puedeRegistrarVisita({ fecha: '2026-11-12', hoy: HOY, franja: almuerzo, hora: '13:00' }), false);
  assert.equal(puedeRegistrarVisita({ fecha: HOY, hoy: HOY, franja: DIA[0], hora: '10:00' }), false);
  assert.equal(puedeRegistrarVisita({ fecha: HOY, hoy: HOY, franja: null, hora: '13:00' }), false);
});

test('validarPersonasVisita: sin mesa hasta 50; con mesa hasta su capacidad', () => {
  assert.equal(validarPersonasVisita('50').valido, true);
  assert.equal(validarPersonasVisita('51').mensaje, 'Sin mesa, el máximo es 50 personas.');
  assert.equal(validarPersonasVisita('4', 4).valido, true);
  assert.equal(validarPersonasVisita('5', 4).mensaje, 'La mesa admite como máximo 4 personas.');
  assert.equal(validarPersonasVisita('0').mensaje, 'Debe haber al menos 1 persona.');
  assert.equal(validarPersonasVisita('2.5').valido, false);
  assert.equal(validarPersonasVisita('').valido, false);
});

test('armarCuerpoVisita: sin datos personales, personas número y mesa vacía como null', () => {
  assert.deepEqual(armarCuerpoVisita({ franjaId: 'f', mesaId: '', personas: '3' }), { franjaHorariaId: 'f', mesaId: null, personas: 3 });
  assert.deepEqual(armarCuerpoVisita({ franjaId: 'f', mesaId: 'm', personas: 2 }), { franjaHorariaId: 'f', mesaId: 'm', personas: 2 });
  assert.deepEqual(Object.keys(armarCuerpoVisita({ franjaId: 'f', personas: 1 })).sort(), ['franjaHorariaId', 'mesaId', 'personas']);
});

const MAPA_L4 = {
  fecha: '2026-11-11', franjaId: 'f1', franjaNombre: 'Almuerzo', horaInicio: '12:00:00', horaFin: '15:00:00',
  zonas: [
    {
      zonaId: 'z2', nombre: 'Salón', activa: false,
      mesas: [{ mesaId: 'm9', numero: 9, capacidad: 4, estado: 'Inactiva', reservaId: null }],
    },
    {
      zonaId: 'z1', nombre: 'Patio', activa: true,
      mesas: [
        { mesaId: 'm3', numero: 3, capacidad: 4, estado: 'Ocupada', reservaId: 'r3', clienteNombre: 'Luis', personas: 4, personasReales: 3 },
        { mesaId: 'm1', numero: 1, capacidad: 2, estado: 'Libre', reservaId: null },
        { mesaId: 'm2', numero: 2, capacidad: 4, estado: 'Reservada', reservaId: 'r2', clienteNombre: 'Ana', personas: 4, personasReales: null },
        { mesaId: 'm4', numero: 4, capacidad: 6, estado: 'Inactiva', reservaId: 'r4', clienteNombre: 'Eva', personas: 2, personasReales: null },
      ],
    },
  ],
};

test('agruparMesasPorZona ordena zonas por nombre y mesas por número, con su resumen', () => {
  const zonas = agruparMesasPorZona(MAPA_L4);
  assert.deepEqual(zonas.map((z) => z.nombre), ['Patio', 'Salón']);
  assert.deepEqual(zonas[0].mesas.map((m) => m.numero), [1, 2, 3, 4]);
  assert.deepEqual(zonas[0].resumen, { libres: 1, reservadas: 1, ocupadas: 1, inactivas: 1, total: 4 });
  assert.equal(zonas[1].activa, false);
  assert.deepEqual(agruparMesasPorZona(null), []);
});

test('resumenPorZona y resumenGlobal suman los estados de las mesas', () => {
  const zonas = agruparMesasPorZona(MAPA_L4);
  assert.deepEqual(resumenPorZona({ mesas: [] }), { libres: 0, reservadas: 0, ocupadas: 0, inactivas: 0, total: 0 });
  assert.deepEqual(resumenGlobal(zonas), { libres: 1, reservadas: 1, ocupadas: 1, inactivas: 2 });
});

test('esConflictoMesaInactiva: solo una mesa inactiva con reserva vigente', () => {
  const mesas = MAPA_L4.zonas.flatMap((z) => z.mesas);
  assert.deepEqual(mesas.filter(esConflictoMesaInactiva).map((m) => m.numero), [4]);
});

test('accionMesa: abre la reserva si la tiene, registra visita en libres solo si se puede, y nada en el resto', () => {
  const mesas = Object.fromEntries(MAPA_L4.zonas.flatMap((z) => z.mesas).map((m) => [m.numero, m]));
  assert.equal(accionMesa(mesas[2], { puedeVisita: true }), 'abrirReserva');
  assert.equal(accionMesa(mesas[3], { puedeVisita: false }), 'abrirReserva');
  assert.equal(accionMesa(mesas[4], { puedeVisita: false }), 'abrirReserva');
  assert.equal(accionMesa(mesas[1], { puedeVisita: true }), 'registrarVisita');
  assert.equal(accionMesa(mesas[1], { puedeVisita: false }), null);
  assert.equal(accionMesa(mesas[9], { puedeVisita: true }), null);
});

test('filaDesdeMesa: fila parcial con lo que trae el mapa y sin código, teléfono ni origen', () => {
  const patio = MAPA_L4.zonas[1];
  const mesa = (numero) => patio.mesas.find((m) => m.numero === numero);
  const fila = filaDesdeMesa(MAPA_L4, patio, mesa(2)); // Reservada
  assert.equal(fila.id, 'r2');
  assert.equal(fila.estado, 'Confirmada');
  assert.equal(fila.mesaNumero, 2);
  assert.equal(fila.zonaNombre, 'Patio');
  assert.equal(fila.franjaNombre, 'Almuerzo');
  assert.equal(fila.clienteNombre, 'Ana');
  assert.equal(fila.codigo, '');
  assert.equal(fila.clienteTelefono, '');
  assert.equal(fila.origen, '');
  assert.equal(filaDesdeMesa(MAPA_L4, patio, mesa(3)).estado, 'EnCurso'); // Ocupada
  assert.equal(filaDesdeMesa(MAPA_L4, patio, mesa(4)).estado, ''); // Inactiva con reserva: estado exacto desconocido
});

// --- Lote 5 (GI-397) ---

test('validarNombreZona: obligatorio y hasta 100 caracteres, con los textos del servidor', () => {
  assert.equal(validarNombreZona('   ').mensaje, 'El nombre de la zona es obligatorio.');
  assert.equal(validarNombreZona('  Patio ').valor, 'Patio');
  assert.equal(validarNombreZona('a'.repeat(100)).valido, true);
  assert.equal(validarNombreZona('a'.repeat(101)).mensaje, 'El nombre de la zona no puede superar los 100 caracteres.');
});

test('validarNombreFranja: obligatorio y hasta 50 caracteres', () => {
  assert.equal(validarNombreFranja('').mensaje, 'El nombre de la franja es obligatorio.');
  assert.equal(validarNombreFranja('a'.repeat(50)).valido, true);
  assert.equal(validarNombreFranja('a'.repeat(51)).mensaje, 'El nombre de la franja no puede superar los 50 caracteres.');
});

test('validarNumeroMesa: entero de 1 en adelante', () => {
  assert.equal(validarNumeroMesa('1').valido, true);
  assert.equal(validarNumeroMesa(' 25 ').valor, 25);
  for (const malo of ['0', '-3', '']) {
    assert.equal(validarNumeroMesa(malo).mensaje, 'El número de la mesa debe ser 1 o mayor.', malo);
  }
  assert.equal(validarNumeroMesa('2.5').mensaje, 'Escribe un número entero.');
  assert.equal(validarNumeroMesa('abc').valido, false);
});

test('validarCapacidadMesa: entero de 1 a 50', () => {
  assert.equal(validarCapacidadMesa('1').valido, true);
  assert.equal(validarCapacidadMesa('50').valido, true);
  for (const malo of ['0', '51', '', '-1']) {
    assert.equal(validarCapacidadMesa(malo).mensaje, 'La capacidad debe estar entre 1 y 50 personas.', malo);
  }
  assert.equal(validarCapacidadMesa('4.5').mensaje, 'Escribe un número entero.');
});

test('validarZonaMesa: la zona es obligatoria', () => {
  assert.equal(validarZonaMesa('abc').valido, true);
  assert.equal(validarZonaMesa('').mensaje, 'Selecciona la zona de la mesa.');
});

test('normalizarHora: HH:mm y H:mm pasan a HH:mm:ss; lo inválido da cadena vacía', () => {
  assert.equal(normalizarHora('09:05'), '09:05:00');
  assert.equal(normalizarHora('9:05'), '09:05:00');
  assert.equal(normalizarHora('22:30:00'), '22:30:00');
  assert.equal(normalizarHora('12:00:15'), '12:00:15');
  assert.equal(normalizarHora('12:00:00.1234567'), '12:00:00');
  assert.equal(normalizarHora(' 18:00 '), '18:00:00');
  for (const malo of ['', '24:00', '12:60', '12', 'ab:cd', '12:00:60', null, undefined]) {
    assert.equal(normalizarHora(malo), '', String(malo));
  }
});

test('validarHorarioFranja: fin debe ser posterior al inicio, y ambas horas obligatorias', () => {
  const ok = validarHorarioFranja('12:00', '15:00');
  assert.equal(ok.valido, true);
  assert.deepEqual([ok.inicio, ok.fin], ['12:00:00', '15:00:00']);
  assert.equal(validarHorarioFranja('15:00', '12:00').errores.fin, 'La hora de fin debe ser posterior a la hora de inicio.');
  assert.equal(validarHorarioFranja('12:00', '12:00').valido, false);
  assert.equal(validarHorarioFranja('', '12:00').errores.inicio, 'Indica la hora de inicio.');
  assert.equal(validarHorarioFranja('12:00', '').errores.fin, 'Indica la hora de fin.');
  assert.equal(validarHorarioFranja('09:00', '09:01').valido, true);
});

test('armarCuerpoZona, armarCuerpoMesa y armarCuerpoFranja: tipos, recorte y horas normalizadas', () => {
  assert.deepEqual(armarCuerpoZona({ nombre: '  Patio  ' }), { nombre: 'Patio' });
  assert.deepEqual(armarCuerpoMesa({ numero: '4', capacidad: '6', zonaId: 'z1' }), { numero: 4, capacidad: 6, zonaId: 'z1' });
  assert.deepEqual(
    armarCuerpoFranja({ nombre: ' Almuerzo ', horaInicio: '12:00', horaFin: '15:30' }),
    { nombre: 'Almuerzo', horaInicio: '12:00:00', horaFin: '15:30:00' },
  );
  assert.equal(typeof armarCuerpoMesa({ numero: '4', capacidad: '6', zonaId: 'z' }).numero, 'number');
});

test('activaDeFiltro: todas = sin filtro, activas = true, inactivas = false', () => {
  assert.equal(activaDeFiltro('todas'), undefined);
  assert.equal(activaDeFiltro(''), undefined);
  assert.equal(activaDeFiltro('activas'), true);
  assert.equal(activaDeFiltro('inactivas'), false);
});

const ZONAS_L5 = [
  { id: 'z2', nombre: 'Terraza', activa: true },
  { id: 'z1', nombre: 'Patio', activa: false },
  { id: 'z3', nombre: 'Barra', activa: true },
];

test('zonasParaMesa: activas más la actual aunque esté inactiva, ordenadas por nombre', () => {
  assert.deepEqual(zonasParaMesa(ZONAS_L5).map((z) => z.id), ['z3', 'z2']);
  const conActual = zonasParaMesa(ZONAS_L5, 'z1');
  assert.deepEqual(conActual.map((z) => z.id), ['z3', 'z1', 'z2']);
  assert.equal(conActual.find((z) => z.id === 'z1').actual, true);
  assert.equal(conActual.find((z) => z.id === 'z2').actual, false);
  assert.deepEqual(zonasParaMesa(undefined), []);
});

test('mesaConZonaInactiva: solo una mesa activa cuya zona está inactiva', () => {
  assert.equal(mesaConZonaInactiva({ activa: true, zonaId: 'z1' }, ZONAS_L5), true);
  assert.equal(mesaConZonaInactiva({ activa: true, zonaId: 'z2' }, ZONAS_L5), false);
  assert.equal(mesaConZonaInactiva({ activa: false, zonaId: 'z1' }, ZONAS_L5), false);
  assert.equal(mesaConZonaInactiva({ activa: true, zonaId: 'desconocida' }, ZONAS_L5), false);
});

test('listaIncompleta: avisa cuando el total supera lo recibido', () => {
  const cien = Array.from({ length: 100 }, () => ({}));
  assert.equal(listaIncompleta({ total: 101, items: cien }), true);
  assert.equal(listaIncompleta({ total: 100, items: cien }), false);
  assert.equal(listaIncompleta({ total: 0, items: [] }), false);
  assert.equal(listaIncompleta(null), false);
});

test('contarMesasZona y textoImpactoZona: solo informan lo que se conoce', () => {
  const completa = contarMesasZona({ total: 3, items: [{ activa: true }, { activa: true }, { activa: false }] });
  assert.deepEqual(completa, { total: 3, activas: 2 });
  assert.equal(textoImpactoZona(completa), 'Esta zona tiene 3 mesas (2 activas).');
  assert.equal(textoImpactoZona({ total: 1, activas: 1 }), 'Esta zona tiene 1 mesa (1 activa).');
  assert.equal(textoImpactoZona({ total: 0, activas: 0 }), 'Esta zona no tiene mesas.');
  const parcial = contarMesasZona({ total: 150, items: Array.from({ length: 100 }, () => ({ activa: true })) });
  assert.deepEqual(parcial, { total: 150, activas: null });
  assert.equal(textoImpactoZona(parcial), 'Esta zona tiene 150 mesas.');
});

test('esConflictoReasignar detecta el 409 de reservas futuras y deja pasar el resto', () => {
  assert.equal(esConflictoReasignar('No se puede desactivar la mesa: tiene 2 reserva(s) futura(s) vigente(s). Reasígnalas antes.'), true);
  assert.equal(esConflictoReasignar('No se puede cambiar el horario: la franja tiene 1 reserva(s) futura(s) vigente(s). Reasígnalas antes.'), true);
  assert.equal(esConflictoReasignar('Ya existe la mesa 4.'), false);
  assert.equal(esConflictoReasignar('El horario se solapa con el de otra franja activa.'), false);
  assert.equal(esConflictoReasignar(undefined), false);
});

test('reemplazarPorId actualiza solo el elemento con ese id', () => {
  const lista = [{ id: 'a', activa: true }, { id: 'b', activa: true }];
  const nueva = reemplazarPorId(lista, { id: 'b', activa: false });
  assert.deepEqual(nueva, [{ id: 'a', activa: true }, { id: 'b', activa: false }]);
  assert.deepEqual(reemplazarPorId(lista, { id: 'zz', activa: false }), lista);
});
