import { useEffect, useState } from 'react';
import { api } from '../security/authService.js';
import './personal.css';

const ruta = '/api/personal/marcaciones';

function mensaje(error, predeterminado) {
  if ([400, 404, 409].includes(error.response?.status) && error.response?.data?.detail)
    return error.response.data.detail;
  return predeterminado;
}

export default function Marcaciones() {
  const [turno, setTurno] = useState(null);
  const [pausa, setPausa] = useState('0');
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
      setExito('La entrada quedó registrada.');
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
      const respuesta = await api.post(`${ruta}/salida`, { minutosPausa: Number(pausa) });
      setTurno(null);
      setPausa('0');
      setExito(`Turno completado: ${respuesta.data.horasTrabajadas} horas registradas.`);
    } catch (fallo) {
      setError(mensaje(fallo, 'No se pudo registrar la salida.'));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div><span className="eyebrow">PERSONAL · GIRA</span><h1>Marcación de turno</h1>
          <p>Registra tu hora real de entrada y salida para el cálculo de horas trabajadas.</p></div>
      </div>
      {error && <p className="notice-error" role="alert">{error}</p>}
      {exito && <p className="form-success" role="status">{exito}</p>}
      {cargando ? <p role="status">Consultando tu turno…</p>
        : turno ? (
          <section className="panel marcacion-panel" aria-labelledby="turno-abierto">
            <h2 id="turno-abierto">Tienes un turno abierto</h2>
            <p>Entrada registrada: <time dateTime={turno.inicioEn}>{new Date(turno.inicioEn).toLocaleString()}</time></p>
            <form onSubmit={marcarSalida}>
              <label className="form-field">
                <span>Minutos de pausa</span>
                <input type="number" min="0" max="720" step="1" value={pausa}
                  onChange={(evento) => setPausa(evento.target.value)} />
              </label>
              <button type="submit" className="button button-primary" disabled={guardando}>
                {guardando ? 'Registrando…' : 'Registrar salida'}
              </button>
            </form>
          </section>
        ) : (
          <section className="panel marcacion-panel">
            <h2>No tienes un turno abierto</h2>
            <button type="button" className="button button-primary" disabled={guardando}
              onClick={marcarEntrada}>
              {guardando ? 'Registrando…' : 'Registrar entrada'}
            </button>
          </section>
        )}
    </section>
  );
}
