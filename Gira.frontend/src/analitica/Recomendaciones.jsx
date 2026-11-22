import { useEffect, useState } from 'react';
import { fecha } from '../inventario/inventarioFormato.js';
import { analiticaService } from './analiticaService.js';
import { mensajeErrorAnalitica, validarRecomendaciones } from './dashboardDatos.js';

export default function Recomendaciones({ recarga }) {
  const [revision, setRevision] = useState(0);
  const [estado, setEstado] = useState({ datos: null, error: '' });
  const [guardando, setGuardando] = useState(null);
  const [errorAccion, setErrorAccion] = useState('');

  useEffect(() => {
    let activo = true;
    setEstado({ datos: null, error: '' });
    analiticaService.recomendaciones()
      .then(validarRecomendaciones)
      .then((datos) => { if (activo) setEstado({ datos, error: '' }); })
      .catch((error) => { if (activo) setEstado({ datos: null, error: mensajeErrorAnalitica(error, 'las recomendaciones') }); });
    return () => { activo = false; };
  }, [recarga, revision]);

  async function cambiarEstado(id, nuevoEstado) {
    setGuardando(id);
    setErrorAccion('');
    try {
      await analiticaService.actualizarRecomendacion(id, nuevoEstado);
      setRevision((valor) => valor + 1);
    } catch (error) {
      setErrorAccion(mensajeErrorAnalitica(error, 'la actualización de la recomendación'));
    } finally {
      setGuardando(null);
    }
  }

  const visibles = estado.datos?.filter((item) => item.estado !== 'Descartada');
  return (
    <section className="panel dashboard-proximamente">
      <h2>Recomendaciones</h2>
      <p>Basadas en los datos disponibles. La falta de registros puede impedir la evaluación de algunas reglas.</p>
      {!estado.datos && !estado.error && <p role="status">Cargando recomendaciones…</p>}
      {estado.error && <div role="alert" className="notice-error">{estado.error}{' '}
        <button type="button" className="button button-secondary button-small" onClick={() => setRevision((v) => v + 1)}>Reintentar recomendaciones</button>
      </div>}
      {errorAccion && <p role="alert" className="notice-error">{errorAccion}</p>}
      {visibles?.length === 0 && <p>No hay recomendaciones vigentes.</p>}
      {visibles?.length > 0 && <ul className="dashboard-recomendaciones">
        {visibles.map((item) => <li key={item.id}>
          <strong>{item.tipo} · {item.prioridad} · {item.estado}</strong>
          <p>{item.mensaje}</p>
          <small>Fecha: {fecha(item.fecha)} · Origen: {item.origen}</small>
          <div className="dashboard-recomendacion-acciones">
            {item.estado === 'Nueva' && <button type="button" className="button button-secondary button-small"
              disabled={guardando !== null} onClick={() => cambiarEstado(item.id, 'Vista')}>Marcar como vista</button>}
            <button type="button" className="button button-secondary button-small"
              disabled={guardando !== null} onClick={() => cambiarEstado(item.id, 'Descartada')}>Descartar</button>
          </div>
        </li>)}
      </ul>}
    </section>
  );
}
