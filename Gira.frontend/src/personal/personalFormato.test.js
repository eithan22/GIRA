import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  aEnvioAusencia,
  aEnvioCargo,
  aEnvioEmpleado,
  aEnvioRequerimiento,
  etiquetaEstado,
  etiquetaEstadoAusencia,
  mensajeDeError,
  validarAusencia,
  validarCargo,
  validarEmpleado,
  validarRequerimiento,
} from './personalFormato.js';

const empleadoValido = {
  nombre: 'Ana',
  apellido: 'Pérez',
  cedula: '001-0000001-1',
  telefono: '',
  email: '',
  cargoId: 'c-1',
  fechaIngreso: '2026-01-15',
  horasMaximasSemana: '40',
};

test('un cargo sin nombre no es válido y uno con nombre sí', () => {
  assert.equal(validarCargo({ nombre: '   ' }).nombre, 'El nombre del cargo es obligatorio.');
  assert.deepEqual(validarCargo({ nombre: 'Mesero', descripcion: '' }), {});
});

test('un cargo con nombre de más de 100 caracteres se rechaza', () => {
  assert.ok(validarCargo({ nombre: 'a'.repeat(101) }).nombre);
});

test('un empleado válido no tiene errores', () => {
  assert.deepEqual(validarEmpleado(empleadoValido), {});
});

test('las horas máximas deben estar entre 1 y 168', () => {
  assert.ok(validarEmpleado({ ...empleadoValido, horasMaximasSemana: '0' }).horasMaximasSemana);
  assert.ok(validarEmpleado({ ...empleadoValido, horasMaximasSemana: '169' }).horasMaximasSemana);
  assert.ok(validarEmpleado({ ...empleadoValido, horasMaximasSemana: 'cuarenta' }).horasMaximasSemana);
  assert.deepEqual(validarEmpleado({ ...empleadoValido, horasMaximasSemana: '168' }), {});
});

test('el correo, si se escribe, debe tener formato válido', () => {
  assert.ok(validarEmpleado({ ...empleadoValido, email: 'ana@mal' }).email);
  assert.deepEqual(validarEmpleado({ ...empleadoValido, email: 'ana@gira.do' }), {});
});

test('sin cargo, sin cédula o sin fecha de ingreso se marcan los campos', () => {
  const errores = validarEmpleado({ ...empleadoValido, cargoId: '', cedula: '', fechaIngreso: '' });
  assert.ok(errores.cargoId);
  assert.ok(errores.cedula);
  assert.ok(errores.fechaIngreso);
});

test('el envío limpia espacios y manda null en los opcionales vacíos', () => {
  const envio = aEnvioEmpleado({ ...empleadoValido, nombre: '  Ana  ', telefono: '  ' });
  assert.equal(envio.nombre, 'Ana');
  assert.equal(envio.telefono, null);
  assert.equal(envio.email, null);
  assert.equal(envio.horasMaximasSemana, 40);
});

test('el envío de un cargo manda descripción null si está vacía', () => {
  assert.deepEqual(aEnvioCargo({ nombre: ' Mesero ', descripcion: ' ' }), { nombre: 'Mesero', descripcion: null });
});

test('el mensaje de error usa el detalle del servidor cuando existe', () => {
  const conDetalle = { response: { data: { detail: 'Ya existe un empleado con esa cédula.' } } };
  assert.equal(mensajeDeError(conDetalle, 'No se pudo guardar.'), 'Ya existe un empleado con esa cédula.');
  assert.equal(mensajeDeError({ response: { data: {} } }, 'No se pudo guardar.'), 'No se pudo guardar.');
  assert.equal(mensajeDeError(new Error('red'), 'No se pudo guardar.'), 'No se pudo guardar.');
});

test('la etiqueta de estado muestra Activo o Inactivo', () => {
  assert.equal(etiquetaEstado(true), 'Activo');
  assert.equal(etiquetaEstado(false), 'Inactivo');
});

test('una ausencia con fechas invertidas o sin empleado no es válida', () => {
  const base = { empleadoId: 'e-1', fechaDesde: '2026-12-07', fechaHasta: '2026-12-08', tipo: '1', motivo: '' };
  assert.deepEqual(validarAusencia(base), {});
  assert.ok(validarAusencia({ ...base, fechaHasta: '2026-12-01' }).fechaHasta);
  assert.ok(validarAusencia({ ...base, empleadoId: '' }).empleadoId);
});

test('el envío de una ausencia convierte el tipo a número y manda null sin motivo', () => {
  const envio = aEnvioAusencia({ empleadoId: 'e-1', fechaDesde: '2026-12-07', fechaHasta: '2026-12-07', tipo: '2', motivo: ' ' });
  assert.equal(envio.tipo, 2);
  assert.equal(envio.motivo, null);
});

test('las etiquetas de estado de ausencia usan los números de la API', () => {
  assert.equal(etiquetaEstadoAusencia(0), 'Solicitada');
  assert.equal(etiquetaEstadoAusencia(1), 'Aprobada');
  assert.equal(etiquetaEstadoAusencia(2), 'Rechazada');
  assert.equal(etiquetaEstadoAusencia(9), 'Desconocido');
});

test('un requerimiento necesita cargo y valores mayores que cero', () => {
  assert.deepEqual(validarRequerimiento({ cargoId: 'c-1', comensalesPorEmpleado: '20', minimoPorFranja: '2' }), {});
  assert.ok(validarRequerimiento({ cargoId: 'c-1', comensalesPorEmpleado: '0', minimoPorFranja: '2' }).comensalesPorEmpleado);
  assert.ok(validarRequerimiento({ cargoId: '', comensalesPorEmpleado: '20', minimoPorFranja: '2' }).cargoId);
});

test('el envío de un requerimiento convierte los valores a número', () => {
  assert.deepEqual(
    aEnvioRequerimiento({ cargoId: 'c-1', comensalesPorEmpleado: '20', minimoPorFranja: '2' }),
    { cargoId: 'c-1', comensalesPorEmpleado: 20, minimoPorFranja: 2 },
  );
});
