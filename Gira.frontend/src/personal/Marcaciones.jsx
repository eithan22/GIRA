import { useEffect, useState } from 'react';
import { api } from '../security/authService.js';
import './personal.css';
import { formatearHoras } from './personalFormato.js';

const ruta = '/api/personal/marcaciones';

function mensaje(error, predeterminado) {
  if ([400, 404, 409].includes(error.response?.status) && error.response?.data?.detail)
    return error.response.data.detail;
  return predeterminado;
}

/** Hora de 24 horas (HH:mm), igual que en el resto del módulo. */
function horaDe(fechaIso) {
  return new Date(fechaIso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
}

export default function Marcaciones() {
  const [turno, setTurno] = useState(null);
  const [pausa, setPausa] = useState('');
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState('');
  const [exito, setExito] = useState('');

  useEffect(() => {
    let activo = true;
    api.get(`${ruta}/actual`)
      .then((respuesta) => { if (activo) setTurno(respuesta.data); })
      .catch((fallo) => {
        if (activo && fallo.response?.status !== 404)
          setError(mensaje(fallo, 'No se pudo consultar el turno actual.'));
      })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, []);

  async function marcarEntrada() {
    setGuardando(true);
    setError('');
    setExito('');
    try {
      const respuesta = await api.post(`${ruta}/entrada`);
      setTurno(respuesta.data);
      setExito('Entrada registrada. Que tengas buen turno.');
    } catch (fallo) {
      setError(mensaje(fallo, 'No se pudo registrar la entrada.'));
    } finally {
      setGuardando(false);
    }
  }

  async function marcarSalida(evento) {
    evento.preventDefault();
    setGuardando(true);
    setError('');
    setExito('');
    try {
      // Si el campo de pausa está vacío, la pausa es 0.
      const respuesta = await api.post(`${ruta}/salida`, { minutosPausa: Number(pausa || 0) });
      setTurno(null);
      setPausa('');
      setExito(`Salida registrada. Trabajaste ${formatearHoras(respuesta.data.horasTrabajadas)}.`);
    } catch (fallo) {
      setError(mensaje(fallo, 'No se pudo registrar la salida.'));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL · GIRA</span>
          <h1>Marcación de turno</h1>
          <p>Marca tu entrada al llegar y tu salida al terminar.</p>
        </div>
      </div>

      {error && <p className="notice-error" role="alert">{error}</p>}
      {exito && <p className="form-success" role="status">{exito}</p>}

      {cargando ? <p role="status">Consultando tu turno…</p> : (
        <section className="panel marcacion-panel" aria-label="Estado de tu turno">
          {turno ? (
            <>
              <p className="marcacion-estado">
                <span className="marcacion-punto activo" aria-hidden="true" />
                Trabajando desde las <time dateTime={turno.inicioEn}>{horaDe(turno.inicioEn)}</time>
              </p>
              <form className="marcacion-form" onSubmit={marcarSalida}>
                <label className="form-field">
                  <span>Minutos de pausa (opcional)</span>
                  <input type="number" min="0" max="720" step="1" inputMode="numeric" placeholder="0"
                    value={pausa} onChange={(evento) => setPausa(evento.target.value)} />
                  <span className="field-hint">Tiempo que no trabajaste, por ejemplo el almuerzo. Déjalo vacío si no tuviste pausa.</span>
                </label>
                <button type="submit" className="button button-primary" disabled={guardando}>
                  {guardando ? 'Registrando…' : 'Registrar salida'}
                </button>
              </form>
            </>
          ) : (
            <>
              <p className="marcacion-estado">
                <span className="marcacion-punto" aria-hidden="true" />
                Sin turno abierto
              </p>
              <button type="button" className="button button-primary" disabled={guardando} onClick={marcarEntrada}>
                {guardando ? 'Registrando…' : 'Registrar entrada'}
              </button>
            </>
          )}
        </section>
      )}
    </section>
  );
}
