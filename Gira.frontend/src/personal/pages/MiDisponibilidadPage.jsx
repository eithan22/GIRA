import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { mensajeDeError } from '../personalFormato.js';
import {
  DIAS_EN_ORDEN,
  aEnvioDisponibilidad,
  nombreDia,
  ordenarDisponibilidad,
  validarDisponibilidad,
} from '../miPersonalFormato.js';
import { horaCorta } from '../planificacionFormato.js';
import '../personal.css';
import '../planificacion.css';

const FORM_VACIO = { diaSemana: '', horaInicio: '', horaFin: '' };

// Mi disponibilidad semanal (GI-363, GI-321, RF-PER-03). Cada empleado registra y quita solo sus propios bloques.
export default function MiDisponibilidadPage() {
  const [bloques, setBloques] = useState([]);
  const [form, setForm] = useState(FORM_VACIO);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);

  async function cargar() {
    setCargando(true);
    try {
      setBloques(await personalService.listarMiDisponibilidad());
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo cargar tu disponibilidad.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, []);

  async function agregar(evento) {
    evento.preventDefault();
    const erroresForm = validarDisponibilidad(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    setGuardando(true);
    try {
      await personalService.agregarMiDisponibilidad(aEnvioDisponibilidad(form));
      setMensaje('Bloque registrado.');
      setForm(FORM_VACIO);
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo registrar el bloque.'));
    } finally {
      setGuardando(false);
    }
  }

  async function quitar(bloque) {
    try {
      await personalService.eliminarMiDisponibilidad(bloque.id);
      setMensaje('Bloque quitado.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo quitar el bloque.'));
    }
  }

  const ordenados = ordenarDisponibilidad(bloques);

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Mi disponibilidad</h1>
          <p>Indica en qué horas de la semana puedes trabajar. Solo se te asignan turnos dentro de estos bloques.</p>
        </div>
      </div>

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>Nuevo bloque</h2>
            <p>Los bloques de un mismo día no pueden solaparse.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={agregar} noValidate>
          <div className="form-grid">
            <div className="form-field">
              <span>Día <i>*</i></span>
              <div className="select-wrap">
                <select value={form.diaSemana} onChange={(e) => setForm({ ...form, diaSemana: e.target.value })}>
                  <option value="">Elige un día</option>
                  {DIAS_EN_ORDEN.map((d) => <option key={d} value={d}>{nombreDia(d)}</option>)}
                </select>
              </div>
            </div>
            <label className="form-field">
              <span>Desde <i>*</i></span>
              <input type="time" value={form.horaInicio} onChange={(e) => setForm({ ...form, horaInicio: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Hasta <i>*</i></span>
              <input type="time" value={form.horaFin} onChange={(e) => setForm({ ...form, horaFin: e.target.value })} />
            </label>
          </div>
          {Object.values(errores).map((texto) => <p key={texto} className="inline-error">{texto}</p>)}
          <div className="form-actions">
            <button type="submit" className="button button-primary" disabled={guardando}>
              {guardando ? 'Guardando…' : 'Agregar bloque'}
            </button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Mis bloques</h2>
            <p>Ordenados de lunes a domingo.</p>
          </div>
          <span className="count-badge">{bloques.length} {bloques.length === 1 ? 'bloque' : 'bloques'}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Día</th><th>Horario</th><th /></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan={3} className="table-message"><span className="button-spinner" /> Cargando tu disponibilidad…</td></tr>
              ) : ordenados.length ? ordenados.map((b) => (
                <tr key={b.id}>
                  <td><strong>{nombreDia(b.diaSemana)}</strong></td>
                  <td>{horaCorta(b.horaInicio)}–{horaCorta(b.horaFin)}</td>
                  <td>
                    <div className="row-actions">
                      <button type="button" className="button button-secondary button-small" onClick={() => quitar(b)}>Quitar</button>
                    </div>
                  </td>
                </tr>
              )) : (
                <tr><td colSpan={3}><div className="empty-state"><strong>No tienes bloques registrados</strong><p>Agrega los días y horas en que puedes trabajar.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
