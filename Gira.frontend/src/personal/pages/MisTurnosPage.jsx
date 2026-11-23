import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { mensajeDeError } from '../personalFormato.js';
import {
  etiquetaEstadoTurno,
  horaCorta,
  lunesDe,
  sumarDias,
} from '../planificacionFormato.js';
import { nombreDia } from '../miPersonalFormato.js';
import '../personal.css';
import '../planificacion.css';

// Fecha de hoy en la zona horaria local, como YYYY-MM-DD.
function hoyLocal() {
  const hoy = new Date();
  const mes = String(hoy.getMonth() + 1).padStart(2, '0');
  const dia = String(hoy.getDate()).padStart(2, '0');
  return `${hoy.getFullYear()}-${mes}-${dia}`;
}

// Mis turnos (GI-363, GI-300, RF-PER-06): solo los míos, por semana. Es de solo lectura.
export default function MisTurnosPage() {
  const [lunes, setLunes] = useState(() => lunesDe(hoyLocal()));
  const [turnos, setTurnos] = useState([]);
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);

  const domingo = sumarDias(lunes, 6);

  async function cargar() {
    setCargando(true);
    try {
      setTurnos(await personalService.listarMisTurnos({ desde: lunes, hasta: domingo }));
      setMensaje('');
    } catch (error) {
      setTurnos([]);
      setMensaje(mensajeDeError(error, 'No se pudieron cargar tus turnos.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, [lunes]);

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Mis turnos</h1>
          <p>Estos son tus turnos de la semana. Si tienes dudas, consulta con tu supervisor.</p>
        </div>
      </div>

      {mensaje && <p className="inline-error" role="alert">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>Semana del {lunes} al {domingo}</h2>
            <p>Se muestran solo los turnos que tienes asignados.</p>
          </div>
        </div>
        <div className="planificador-filtros">
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(sumarDias(lunes, -7))}>Semana anterior</button>
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(lunesDe(hoyLocal()))}>Hoy</button>
          <button type="button" className="button button-secondary button-small" onClick={() => setLunes(sumarDias(lunes, 7))}>Semana siguiente</button>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Día</th><th>Fecha</th><th>Horario</th><th>Estado</th><th>Notas</th></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan={5} className="table-message"><span className="button-spinner" /> Cargando tus turnos…</td></tr>
              ) : turnos.length ? turnos.map((t) => (
                <tr key={t.id}>
                  <td><strong>{nombreDia(new Date(`${t.fecha}T00:00:00`).getDay())}</strong></td>
                  <td>{t.fecha}</td>
                  <td>{horaCorta(t.horaInicio)}–{horaCorta(t.horaFin)}</td>
                  <td>{etiquetaEstadoTurno(t.estado)}</td>
                  <td>{t.notas ?? '—'}</td>
                </tr>
              )) : (
                <tr><td colSpan={5}><div className="empty-state"><strong>No tienes turnos esta semana</strong><p>Cuando te asignen turnos aparecerán aquí.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
