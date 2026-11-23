import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import {
  ESTADO_AUSENCIA,
  TIPOS_AUSENCIA_OPCIONES,
  etiquetaEstadoAusencia,
  mensajeDeError,
} from '../personalFormato.js';
import {
  aEnvioSolicitudPropia,
  validarSolicitudPropia,
} from '../miPersonalFormato.js';
import '../personal.css';
import '../planificacion.css';

const FORM_VACIO = { fechaDesde: '', fechaHasta: '', tipo: String(TIPOS_AUSENCIA_OPCIONES[0].valor), motivo: '' };

// Mis ausencias (GI-367, RF-PER-11): el empleado pide sus ausencias y ve el estado de cada una.
export default function MisAusenciasPage() {
  const [ausencias, setAusencias] = useState([]);
  const [form, setForm] = useState(FORM_VACIO);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);

  async function cargar() {
    setCargando(true);
    try {
      setAusencias(await personalService.listarMisAusencias());
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar tus ausencias.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, []);

  async function solicitar(evento) {
    evento.preventDefault();
    const erroresForm = validarSolicitudPropia(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    setGuardando(true);
    try {
      await personalService.solicitarMiAusencia(aEnvioSolicitudPropia(form));
      setMensaje('Solicitud enviada. Queda pendiente hasta que la revise tu gerente.');
      setForm(FORM_VACIO);
      setErrores({});
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo enviar la solicitud.'));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Mis ausencias</h1>
          <p>Pide vacaciones, permisos o licencias y revisa en qué estado está cada solicitud.</p>
        </div>
      </div>

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>Nueva solicitud</h2>
            <p>Las fechas incluyen el día de inicio y el de fin.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={solicitar} noValidate>
          <div className="form-grid">
            <label className="form-field">
              <span>Tipo <i>*</i></span>
              <div className="select-wrap">
                <select value={form.tipo} onChange={(e) => setForm({ ...form, tipo: e.target.value })}>
                  {TIPOS_AUSENCIA_OPCIONES.map((t) => <option key={t.valor} value={t.valor}>{t.etiqueta}</option>)}
                </select>
              </div>
            </label>
            <label className="form-field">
              <span>Desde <i>*</i></span>
              <input type="date" value={form.fechaDesde} onChange={(e) => setForm({ ...form, fechaDesde: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Hasta <i>*</i></span>
              <input type="date" value={form.fechaHasta} onChange={(e) => setForm({ ...form, fechaHasta: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Motivo</span>
              <input value={form.motivo} onChange={(e) => setForm({ ...form, motivo: e.target.value })} maxLength={250} />
            </label>
          </div>
          {Object.values(errores).map((texto) => <p key={texto} className="inline-error">{texto}</p>)}
          <div className="form-actions">
            <button type="submit" className="button button-primary" disabled={guardando}>
              {guardando ? 'Enviando…' : 'Enviar solicitud'}
            </button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Mis solicitudes</h2>
            <p>Solo tú ves tus solicitudes.</p>
          </div>
          <span className="count-badge">{ausencias.length} {ausencias.length === 1 ? 'solicitud' : 'solicitudes'}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Tipo</th><th>Desde</th><th>Hasta</th><th>Motivo</th><th>Estado</th></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan={5} className="table-message"><span className="button-spinner" /> Cargando tus ausencias…</td></tr>
              ) : ausencias.length ? ausencias.map((a) => (
                <tr key={a.id}>
                  <td>{TIPOS_AUSENCIA_OPCIONES.find((t) => t.valor === a.tipo)?.etiqueta ?? '—'}</td>
                  <td>{a.fechaDesde}</td>
                  <td>{a.fechaHasta}</td>
                  <td>{a.motivo ?? '—'}</td>
                  <td><span className={`status-pill ${a.estado === ESTADO_AUSENCIA.APROBADA ? 'active' : 'inactive'}`}><span className="status-dot" />{etiquetaEstadoAusencia(a.estado)}</span></td>
                </tr>
              )) : (
                <tr><td colSpan={5}><div className="empty-state"><strong>No tienes solicitudes</strong><p>Envía una con el formulario de arriba.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
