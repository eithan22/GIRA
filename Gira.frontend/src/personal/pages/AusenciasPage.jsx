import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import {
  ESTADO_AUSENCIA,
  TIPO_AUSENCIA,
  TIPOS_AUSENCIA_OPCIONES,
  aEnvioAusencia,
  etiquetaEstadoAusencia,
  mensajeDeError,
  validarAusencia,
} from '../personalFormato.js';
import PersonalTabs from '../components/PersonalTabs.jsx';
import '../personal.css';

const FORM_VACIO = { empleadoId: '', fechaDesde: '', fechaHasta: '', tipo: String(TIPO_AUSENCIA.VACACIONES), motivo: '' };

// Ausencias (GI-367, RF-PER-11): registrar solicitudes y aprobarlas o rechazarlas.
export default function AusenciasPage() {
  const [ausencias, setAusencias] = useState([]);
  const [empleados, setEmpleados] = useState([]);
  const [form, setForm] = useState(FORM_VACIO);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);
  const [resolviendo, setResolviendo] = useState(null);

  async function cargar() {
    setCargando(true);
    try {
      const [listaAusencias, listaEmpleados] = await Promise.all([
        personalService.listarAusencias(),
        personalService.listarEmpleados({ soloActivos: true }),
      ]);
      setAusencias(listaAusencias);
      setEmpleados(listaEmpleados);
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar las ausencias.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, []);

  const nombreEmpleado = (id) => {
    const e = empleados.find((x) => x.id === id);
    return e ? `${e.nombre} ${e.apellido}` : 'Empleado';
  };

  async function registrar(evento) {
    evento.preventDefault();
    const erroresForm = validarAusencia(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    try {
      await personalService.crearAusencia(aEnvioAusencia(form));
      setMensaje('Solicitud registrada.');
      setForm(FORM_VACIO);
      setErrores({});
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo registrar la ausencia.'));
    }
  }

  async function resolver(ausencia, estado) {
    setResolviendo(ausencia.id);
    try {
      await personalService.resolverAusencia(ausencia.id, estado);
      setMensaje(estado === ESTADO_AUSENCIA.APROBADA ? 'Ausencia aprobada.' : 'Ausencia rechazada.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo resolver la solicitud.'));
    } finally {
      setResolviendo(null);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Ausencias</h1>
          <p>Registra las solicitudes de ausencia y decide si se aprueban. Una ausencia aprobada impide asignar turnos esos días.</p>
        </div>
      </div>

      <PersonalTabs />

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>Nueva solicitud</h2>
            <p>Las fechas incluyen el día de inicio y el de fin.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={registrar} noValidate>
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
            <button type="submit" className="button button-primary">Registrar solicitud</button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Solicitudes</h2>
            <p>Solo se aprueban o rechazan las solicitudes pendientes.</p>
          </div>
          <span className="count-badge">{ausencias.length} {ausencias.length === 1 ? 'solicitud' : 'solicitudes'}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Empleado</th><th>Tipo</th><th>Desde</th><th>Hasta</th><th>Estado</th><th /></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="6" className="table-message"><span className="button-spinner" /> Cargando ausencias…</td></tr>
              ) : ausencias.length ? ausencias.map((a) => (
                <tr key={a.id}>
                  <td><strong>{nombreEmpleado(a.empleadoId)}</strong></td>
                  <td>{TIPOS_AUSENCIA_OPCIONES.find((t) => t.valor === a.tipo)?.etiqueta ?? '—'}</td>
                  <td>{a.fechaDesde}</td>
                  <td>{a.fechaHasta}</td>
                  <td><span className={`status-pill ${a.estado === ESTADO_AUSENCIA.APROBADA ? 'active' : 'inactive'}`}><span className="status-dot" />{etiquetaEstadoAusencia(a.estado)}</span></td>
                  <td>
                    {a.estado === ESTADO_AUSENCIA.SOLICITADA && (
                      <div className="row-actions">
                        <button type="button" className="button button-primary button-small" disabled={resolviendo === a.id} onClick={() => resolver(a, ESTADO_AUSENCIA.APROBADA)}>Aprobar</button>
                        <button type="button" className="button button-secondary button-small" disabled={resolviendo === a.id} onClick={() => resolver(a, ESTADO_AUSENCIA.RECHAZADA)}>Rechazar</button>
                      </div>
                    )}
                  </td>
                </tr>
              )) : (
                <tr><td colSpan="6"><div className="empty-state"><strong>No hay solicitudes</strong><p>Registra una con el formulario de arriba.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
