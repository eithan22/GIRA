import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { mensajeDeError } from '../personalFormato.js';
import {
  ESTADO_TURNO,
  aEnvioTurno,
  diasDeSemana,
  etiquetaEstadoTurno,
  horaCorta,
  lunesDe,
  sumarDias,
  turnosQueCubren,
  validarTurno,
} from '../planificacionFormato.js';
import '../personal.css';
import '../planificacion.css';

const FORM_VACIO = { empleadoId: '', cargoId: '', fecha: '', horaInicio: '', horaFin: '', notas: '' };

// Fecha de hoy en la zona horaria local, como YYYY-MM-DD.
function hoyLocal() {
  const hoy = new Date();
  const mes = String(hoy.getMonth() + 1).padStart(2, '0');
  const dia = String(hoy.getDate()).padStart(2, '0');
  return `${hoy.getFullYear()}-${mes}-${dia}`;
}

// Planificador semanal (GI-364, RF-PER-04 y RF-PER-07): cuadrícula por día, franja y cargo, con asignar,
// modificar y cancelar turnos. Las reglas las revisa el API; aquí solo se muestra su motivo si rechaza.
export default function PlanificadorPage() {
  const [lunes, setLunes] = useState(() => lunesDe(hoyLocal()));
  const [cargoFiltro, setCargoFiltro] = useState('');
  const [cobertura, setCobertura] = useState([]);
  const [turnos, setTurnos] = useState([]);
  const [empleados, setEmpleados] = useState([]);
  const [cargos, setCargos] = useState([]);
  const [form, setForm] = useState(FORM_VACIO);
  const [editandoId, setEditandoId] = useState(null);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);

  const domingo = sumarDias(lunes, 6);

  async function cargar() {
    setCargando(true);
    const cargoId = cargoFiltro || undefined;
    try {
      const [cob, lista, emps, cgs] = await Promise.all([
        personalService.obtenerCobertura({ desde: lunes, hasta: domingo, cargoId }),
        personalService.listarTurnos({ desde: lunes, hasta: domingo, cargoId }),
        personalService.listarEmpleados({ soloActivos: true }),
        personalService.listarCargos(),
      ]);
      setCobertura(cob);
      setTurnos(lista);
      setEmpleados(emps);
      setCargos(cgs);
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo cargar la planificación.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, [lunes, cargoFiltro]);

  const nombreEmpleado = (id) => {
    const e = empleados.find((x) => x.id === id);
    return e ? `${e.nombre} ${e.apellido}` : 'Empleado';
  };

  const nombreCargo = (id) => cargos.find((c) => c.id === id)?.nombre ?? 'Cargo';

  // Filas de la cuadrícula: un renglón por cargo y franja, tomados de la cobertura.
  const filas = [];
  const vistas = new Set();
  cobertura.forEach((c) => {
    const clave = `${c.cargoId}|${c.franjaHorariaId}`;
    if (!vistas.has(clave)) {
      vistas.add(clave);
      filas.push({ cargoId: c.cargoId, franjaId: c.franjaHorariaId, franjaNombre: c.franjaNombre, horaInicio: c.horaInicio, horaFin: c.horaFin, cargoNombre: c.cargoNombre });
    }
  });
  filas.sort((a, b) => a.cargoNombre.localeCompare(b.cargoNombre) || a.horaInicio.localeCompare(b.horaInicio));
  const dias = diasDeSemana(lunes);

  function editar(turno) {
    setEditandoId(turno.id);
    setForm({
      empleadoId: turno.empleadoId,
      cargoId: turno.cargoId,
      fecha: turno.fecha,
      horaInicio: horaCorta(turno.horaInicio),
      horaFin: horaCorta(turno.horaFin),
      notas: turno.notas ?? '',
    });
    setErrores({});
    setMensaje('');
  }

  function limpiar() {
    setEditandoId(null);
    setForm(FORM_VACIO);
    setErrores({});
  }

  async function guardar(evento) {
    evento.preventDefault();
    const erroresForm = validarTurno(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    setGuardando(true);
    try {
      const datos = aEnvioTurno(form);
      if (editandoId) await personalService.actualizarTurno(editandoId, datos);
      else await personalService.crearTurno(datos);
      setMensaje(editandoId ? 'Turno modificado.' : 'Turno asignado.');
      limpiar();
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo guardar el turno.'));
    } finally {
      setGuardando(false);
    }
  }

  async function cancelar(turno) {
    try {
      await personalService.cambiarEstadoTurno(turno.id, ESTADO_TURNO.CANCELADO);
      setMensaje('Turno cancelado.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo cancelar el turno.'));
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Planificador semanal</h1>
          <p>Revisa la cobertura de cada franja y cargo, y asigna, modifica o cancela los turnos de la semana.</p>
        </div>
      </div>

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>Semana del {lunes} al {domingo}</h2>
            <p>Cada celda muestra cuántas personas cubren la franja completa, y sus nombres.</p>
          </div>
        </div>
        <div className="planificador-filtros">
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(sumarDias(lunes, -7))}>Semana anterior</button>
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(lunesDe(hoyLocal()))}>Hoy</button>
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(sumarDias(lunes, 7))}>Semana siguiente</button>
          <label className="field-hint">
            Cargo{' '}
            <select value={cargoFiltro} onChange={(e) => setCargoFiltro(e.target.value)} aria-label="Filtrar por cargo">
              <option value="">Todos</option>
              {cargos.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
            </select>
          </label>
        </div>
        <div className="table-scroll">
          <table className="data-table planificador-grid">
            <thead>
              <tr>
                <th>Cargo · franja</th>
                {dias.map((d) => <th key={d}>{d}</th>)}
              </tr>
            </thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan={8} className="table-message"><span className="button-spinner" /> Cargando planificación…</td></tr>
              ) : filas.length ? filas.map((fila) => (
                <tr key={`${fila.cargoId}|${fila.franjaId}`}>
                  <td><strong>{fila.cargoNombre}</strong><br />{fila.franjaNombre} · {horaCorta(fila.horaInicio)}–{horaCorta(fila.horaFin)}</td>
                  {dias.map((fecha) => {
                    const c = cobertura.find((x) => x.fecha === fecha && x.franjaHorariaId === fila.franjaId && x.cargoId === fila.cargoId);
                    const nombres = turnosQueCubren(turnos, { fecha, cargoId: fila.cargoId, horaInicio: fila.horaInicio, horaFin: fila.horaFin })
                      .map((t) => nombreEmpleado(t.empleadoId))
                      .join(', ');
                    return (
                      <td key={fecha}>
                        <div className="celda-cobertura">
                          <strong>{c?.personalPlanificado ?? 0}</strong>
                          <span className="celda-nombres">{nombres || '—'}</span>
                        </div>
                      </td>
                    );
                  })}
                </tr>
              )) : (
                <tr><td colSpan={8}><div className="empty-state"><strong>No hay franjas para mostrar</strong><p>Configura las franjas en Reservas y los cargos en la pestaña Cargos.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>{editandoId ? 'Modificar turno' : 'Asignar turno'}</h2>
            <p>El API rechaza el turno si cae fuera de la disponibilidad, se solapa, choca con una ausencia o supera las horas de la semana.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={guardar} noValidate>
          <div className="form-grid">
            <div className="form-field">
              <span>Empleado <i>*</i></span>
              <div className="select-wrap">
                <select value={form.empleadoId} onChange={(e) => setForm({ ...form, empleadoId: e.target.value })}>
                  <option value="">Elige un empleado</option>
                  {empleados.map((e) => <option key={e.id} value={e.id}>{e.nombre} {e.apellido}</option>)}
                </select>
              </div>
            </div>
            <div className="form-field">
              <span>Cargo <i>*</i></span>
              <div className="select-wrap">
                <select value={form.cargoId} onChange={(e) => setForm({ ...form, cargoId: e.target.value })}>
                  <option value="">Elige un cargo</option>
                  {cargos.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                </select>
              </div>
            </div>
            <label className="form-field">
              <span>Fecha <i>*</i></span>
              <input type="date" value={form.fecha} onChange={(e) => setForm({ ...form, fecha: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Inicio <i>*</i></span>
              <input type="time" value={form.horaInicio} onChange={(e) => setForm({ ...form, horaInicio: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Fin <i>*</i></span>
              <input type="time" value={form.horaFin} onChange={(e) => setForm({ ...form, horaFin: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Notas</span>
              <input value={form.notas} onChange={(e) => setForm({ ...form, notas: e.target.value })} maxLength={250} />
            </label>
          </div>
          {Object.values(errores).map((texto) => <p key={texto} className="inline-error">{texto}</p>)}
          <div className="form-actions">
            {editandoId && <button type="button" className="button button-secondary" onClick={limpiar}>Cancelar edición</button>}
            <button type="submit" className="button button-primary" disabled={guardando}>
              {guardando ? 'Guardando…' : editandoId ? 'Guardar cambios' : 'Asignar turno'}
            </button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Turnos de la semana</h2>
            <p>Solo los turnos vigentes se pueden modificar o cancelar.</p>
          </div>
          <span className="count-badge">{turnos.length} {turnos.length === 1 ? 'turno' : 'turnos'}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Fecha</th><th>Empleado</th><th>Cargo</th><th>Horario</th><th>Estado</th><th /></tr></thead>
            <tbody>
              {turnos.length ? turnos.map((t) => {
                const vigente = t.estado === ESTADO_TURNO.PROGRAMADO || t.estado === ESTADO_TURNO.CONFIRMADO;
                return (
                  <tr key={t.id}>
                    <td>{t.fecha}</td>
                    <td><strong>{nombreEmpleado(t.empleadoId)}</strong></td>
                    <td>{nombreCargo(t.cargoId)}</td>
                    <td>{horaCorta(t.horaInicio)}–{horaCorta(t.horaFin)}</td>
                    <td><span className={`status-pill ${vigente ? 'active' : 'inactive'}`}><span className="status-dot" />{etiquetaEstadoTurno(t.estado)}</span></td>
                    <td>
                      {vigente && (
                        <div className="row-actions">
                          <button type="button" className="button button-secondary button-small" onClick={() => editar(t)}>Modificar</button>
                          <button type="button" className="button button-secondary button-small" onClick={() => cancelar(t)}>Cancelar</button>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              }) : (
                <tr><td colSpan={6}><div className="empty-state"><strong>No hay turnos en la semana</strong><p>Asigna el primero con el formulario de arriba.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
