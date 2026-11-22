import { useEffect, useState } from 'react';
import { fecha } from '../inventario/inventarioFormato.js';
import { analiticaService } from './analiticaService.js';
import { mensajeErrorAnalitica } from './dashboardDatos.js';

export default function CalendarioFeriados({ hoy, recarga }) {
  const [revision, setRevision] = useState(0);
  const [feriados, setFeriados] = useState(null);
  const [fechaFeriado, setFechaFeriado] = useState('');
  const [motivo, setMotivo] = useState('');
  const [quitando, setQuitando] = useState(null);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState('');
  const [exito, setExito] = useState('');

  useEffect(() => {
    let activo = true;
    setFeriados(null);
    setError('');
    analiticaService.feriados()
      .then((respuesta) => {
        if (!Array.isArray(respuesta)) throw new Error('La API devolvió un calendario de feriados inválido.');
        if (activo) setFeriados(respuesta);
      })
      .catch((fallo) => {
        if (activo) setError(mensajeErrorAnalitica(fallo, 'el calendario de feriados'));
      });
    return () => { activo = false; };
  }, [recarga, revision]);

  async function crear(evento) {
    evento.preventDefault();
    setGuardando(true);
    setError('');
    setExito('');
    try {
      const respuesta = await analiticaService.crearFeriado({ fecha: fechaFeriado, motivo: motivo.trim() || null });
      if (!respuesta?.diaCerrado) throw new Error('La API no confirmó el feriado creado.');
      setFeriados((actuales) => [...(actuales ?? []), respuesta.diaCerrado]
        .sort((a, b) => a.fecha.localeCompare(b.fecha)));
      setFechaFeriado('');
      setMotivo('');
      setExito(respuesta.advertencias?.join(' ') || 'El feriado se agregó al calendario.');
    } catch (fallo) {
      setError(mensajeErrorAnalitica(fallo, 'la creación del feriado'));
    } finally {
      setGuardando(false);
    }
  }

  async function eliminar(id) {
    setGuardando(true);
    setError('');
    setExito('');
    try {
      await analiticaService.eliminarFeriado(id);
      setFeriados((actuales) => actuales.filter((feriado) => feriado.id !== id));
      setQuitando(null);
      setExito('El feriado se quitó del calendario.');
    } catch (fallo) {
      setError(mensajeErrorAnalitica(fallo, 'la eliminación del feriado'));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section className="panel dashboard-feriados">
      <div className="panel-heading"><div><h2>Calendario de feriados y cierres</h2>
        <p>Las fechas cerradas no generan predicciones ni aceptan nuevas reservas.</p></div></div>
      {error && <div className="notice-error" role="alert">{error}{' '}
        <button type="button" className="button button-secondary button-small" onClick={() => setRevision((valor) => valor + 1)}>Reintentar</button>
      </div>}
      {exito && <p className="dashboard-feriados-exito" role="status">{exito}</p>}
      <form className="dashboard-feriados-form" onSubmit={crear}>
        <label>Fecha
          <input type="date" required min={hoy} value={fechaFeriado}
            onChange={(evento) => setFechaFeriado(evento.target.value)} />
        </label>
        <label>Motivo
          <input type="text" maxLength={200} value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)} />
        </label>
        <button type="submit" className="button button-primary" disabled={guardando || !fechaFeriado}>
          {guardando ? 'Guardando…' : 'Agregar fecha'}
        </button>
      </form>
      {feriados === null && !error && <p role="status">Cargando calendario…</p>}
      {feriados?.length === 0 && <p>No hay fechas cerradas próximas.</p>}
      {feriados?.length > 0 && <ul className="dashboard-feriados-lista">
        {[...feriados].sort((a, b) => a.fecha.localeCompare(b.fecha)).map((feriado) => (
          <li key={feriado.id}>
            <div><strong>{fecha(feriado.fecha)}</strong>{feriado.motivo && <span>{feriado.motivo}</span>}</div>
            {quitando === feriado.id
              ? <span className="dashboard-feriados-acciones">
                <span>¿Quitar esta fecha?</span>
                <button type="button" className="button button-secondary button-small"
                  disabled={guardando} onClick={() => eliminar(feriado.id)}>Confirmar</button>
                <button type="button" className="button button-secondary button-small"
                  disabled={guardando} onClick={() => setQuitando(null)}>Cancelar</button>
              </span>
              : <button type="button" className="button button-secondary button-small"
                disabled={guardando} onClick={() => setQuitando(feriado.id)}>Quitar</button>}
          </li>
        ))}
      </ul>}
    </section>
  );
}
