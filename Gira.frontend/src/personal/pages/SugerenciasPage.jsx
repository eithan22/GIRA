import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { mensajeDeError } from '../personalFormato.js';
import {
  ESTADO_SUGERENCIA,
  FILTRO_SUGERENCIAS,
  etiquetaEstadoSugerencia,
  horaCorta,
  puedeResolver,
  textoDiferencia,
} from '../planificacionFormato.js';
import '../personal.css';
import '../planificacion.css';

// Panel de sugerencias de personal (GI-365, RF-PER-09 y RF-PER-10). Aceptar o rechazar no cambia turnos (RN-PER-06).
export default function SugerenciasPage() {
  const [sugerencias, setSugerencias] = useState([]);
  const [filtro, setFiltro] = useState(String(ESTADO_SUGERENCIA.PENDIENTE));
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);
  const [resolviendo, setResolviendo] = useState(null);

  async function cargar() {
    setCargando(true);
    try {
      const estado = filtro === '' ? undefined : Number(filtro);
      setSugerencias(await personalService.listarSugerencias({ estado }));
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar las sugerencias.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, [filtro]);

  async function resolver(sugerencia, estado) {
    setResolviendo(sugerencia.id);
    try {
      await personalService.resolverSugerencia(sugerencia.id, estado);
      setMensaje(estado === ESTADO_SUGERENCIA.ACEPTADA ? 'Sugerencia aceptada.' : 'Sugerencia rechazada.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo resolver la sugerencia.'));
    } finally {
      setResolviendo(null);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Sugerencias de personal</h1>
          <p>Compara el personal requerido con el planificado según la afluencia predicha. Aceptar o rechazar no cambia los turnos: para cambiarlos, usa el planificador.</p>
        </div>
      </div>

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Sugerencias</h2>
            <p>Solo las pendientes se pueden aceptar o rechazar.</p>
          </div>
          <label className="field-hint">
            Estado{' '}
            <select value={filtro} onChange={(e) => setFiltro(e.target.value)} aria-label="Filtrar por estado">
              {FILTRO_SUGERENCIAS.map((f) => <option key={f.valor} value={f.valor}>{f.etiqueta}</option>)}
            </select>
          </label>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr><th>Fecha</th><th>Franja</th><th>Cargo</th><th>Requerido</th><th>Planificado</th><th>Diferencia</th><th>Estado</th><th /></tr>
            </thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="8" className="table-message"><span className="button-spinner" /> Cargando sugerencias…</td></tr>
              ) : sugerencias.length ? sugerencias.map((s) => (
                <tr key={s.id}>
                  <td>{s.fecha}</td>
                  <td>{s.franjaNombre} · {horaCorta(s.horaInicio)}–{horaCorta(s.horaFin)}</td>
                  <td>{s.cargoNombre}</td>
                  <td>{s.personalRequerido}</td>
                  <td>{s.personalPlanificado}</td>
                  <td><strong>{textoDiferencia(s.diferencia)}</strong></td>
                  <td>
                    <span className={`status-pill ${s.estado === ESTADO_SUGERENCIA.ACEPTADA ? 'active' : 'inactive'}`}>
                      <span className="status-dot" />{etiquetaEstadoSugerencia(s.estado)}
                    </span>
                  </td>
                  <td>
                    {puedeResolver(s) && (
                      <div className="row-actions">
                        <button type="button" className="button button-primary button-small" disabled={resolviendo === s.id} onClick={() => resolver(s, ESTADO_SUGERENCIA.ACEPTADA)}>Aceptar</button>
                        <button type="button" className="button button-secondary button-small" disabled={resolviendo === s.id} onClick={() => resolver(s, ESTADO_SUGERENCIA.RECHAZADA)}>Rechazar</button>
                      </div>
                    )}
                  </td>
                </tr>
              )) : (
                <tr><td colSpan="8"><div className="empty-state"><strong>No hay sugerencias</strong><p>Aparecen cuando llega una predicción de afluencia con diferencias de personal.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
