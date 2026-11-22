import { useEffect, useState } from 'react';
import { fechaHora, numero } from '../inventario/inventarioFormato.js';
import { analiticaService } from './analiticaService.js';
import { mensajeErrorAnalitica } from './dashboardDatos.js';

const estadosFinales = new Set(['Completado', 'Fallido']);
const resultadoTexto = {
  DatosInsuficientes: 'Datos insuficientes',
  Activado: 'Modelo activado',
  NoMejoraModeloVigente: 'No mejora el modelo de referencia',
};

export default function ModelosAnalitica({ recarga }) {
  const [revision, setRevision] = useState(0);
  const [modelos, setModelos] = useState(null);
  const [errorModelos, setErrorModelos] = useState('');
  const [errorAccion, setErrorAccion] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [entrenamientoId, setEntrenamientoId] = useState(null);
  const [entrenamiento, setEntrenamiento] = useState(null);
  const [errorEntrenamiento, setErrorEntrenamiento] = useState('');
  const [reintentoEntrenamiento, setReintentoEntrenamiento] = useState(0);
  const [motivoRollback, setMotivoRollback] = useState('');
  const [activando, setActivando] = useState(null);

  useEffect(() => {
    let activo = true;
    setModelos(null);
    setErrorModelos('');
    analiticaService.modelos()
      .then((respuesta) => {
        if (!Array.isArray(respuesta)) throw new Error('La API devolvió un listado de modelos inválido.');
        if (activo) setModelos(respuesta);
      })
      .catch((error) => {
        if (activo) setErrorModelos(mensajeErrorAnalitica(error, 'los modelos de afluencia'));
      });
    return () => { activo = false; };
  }, [recarga, revision]);

  useEffect(() => {
    if (!entrenamientoId) return undefined;
    let activo = true;
    let temporizador;

    async function consultar() {
      try {
        const estado = await analiticaService.entrenamiento(entrenamientoId);
        if (!Array.isArray(estado.resultados)) throw new Error('La API devolvió métricas de entrenamiento inválidas.');
        if (!activo) return;
        setEntrenamiento(estado);
        setErrorEntrenamiento('');
        if (estadosFinales.has(estado.estado)) {
          setEntrenamientoId(null);
          setRevision((valor) => valor + 1);
          return;
        }
        temporizador = setTimeout(consultar, 3000);
      } catch (error) {
        if (activo) setErrorEntrenamiento(mensajeErrorAnalitica(error, 'el estado del entrenamiento'));
      }
    }

    consultar();
    return () => {
      activo = false;
      clearTimeout(temporizador);
    };
  }, [entrenamientoId, reintentoEntrenamiento]);

  async function solicitarEntrenamiento() {
    setGuardando(true);
    setErrorAccion('');
    setErrorEntrenamiento('');
    setEntrenamiento(null);
    try {
      const solicitud = await analiticaService.solicitarEntrenamiento();
      if (!solicitud?.id) throw new Error('La API no devolvió el identificador del entrenamiento.');
      setEntrenamientoId(solicitud.id);
      setEntrenamiento({ id: solicitud.id, estado: solicitud.estado, resultados: [] });
    } catch (error) {
      setErrorAccion(mensajeErrorAnalitica(error, 'la solicitud de entrenamiento'));
    } finally {
      setGuardando(false);
    }
  }

  async function activarModelo(modelo) {
    if (!motivoRollback.trim()) {
      setErrorAccion('Escribe el motivo del rollback para dejarlo auditado.');
      return;
    }
    setActivando(modelo.id);
    setErrorAccion('');
    try {
      await analiticaService.activarModelo(modelo.id, motivoRollback.trim());
      setMotivoRollback('');
      setRevision((valor) => valor + 1);
    } catch (error) {
      setErrorAccion(mensajeErrorAnalitica(error, 'el rollback del modelo'));
    } finally {
      setActivando(null);
    }
  }

  const activoPorFranja = new Map((modelos ?? []).filter((modelo) => modelo.activo)
    .map((modelo) => [modelo.franjaHorariaId, modelo]));
  const anteriores = (modelos ?? []).filter((modelo) => {
    const vigente = activoPorFranja.get(modelo.franjaHorariaId);
    return !modelo.activo && vigente && Date.parse(modelo.creadoEn) < Date.parse(vigente.creadoEn);
  });
  const sinDatosSuficientes = entrenamiento?.estado === 'Completado'
    && entrenamiento.resultados?.length > 0
    && entrenamiento.resultados.every((resultado) => resultado.estado === 'DatosInsuficientes');

  return (
    <section className="panel dashboard-modelos">
      <div className="panel-heading">
        <div><h2>Modelos de afluencia</h2><p>Entrenamiento SSA, métricas de validación y versiones activas.</p></div>
      </div>
      <p>Se requieren 56 días observados para entrenar y 14 días separados para validar por franja. Los días sin registros no se convierten en ceros.</p>
      {errorModelos && <div role="alert" className="notice-error">{errorModelos}{' '}
        <button type="button" className="button button-secondary button-small" onClick={() => setRevision((valor) => valor + 1)}>Reintentar</button>
      </div>}
      {errorAccion && <p role="alert" className="notice-error">{errorAccion}</p>}
      {errorEntrenamiento && <div role="alert" className="notice-error">
        {errorEntrenamiento}{' '}
        <button type="button" className="button button-secondary button-small"
          onClick={() => setReintentoEntrenamiento((valor) => valor + 1)}>Reintentar consulta</button>
      </div>}
      <button type="button" className="button button-primary" disabled={guardando || !!entrenamientoId}
        onClick={solicitarEntrenamiento}>
        {guardando ? 'Solicitando…' : entrenamientoId ? 'Entrenamiento en proceso…' : 'Solicitar entrenamiento'}
      </button>
      {entrenamiento && <div className="dashboard-entrenamiento" role="status">
        <strong>{sinDatosSuficientes
          ? 'Entrenamiento finalizado: datos insuficientes en todas las franjas'
          : `Entrenamiento ${entrenamiento.estado}`}</strong>
        {entrenamiento.error && <p>{entrenamiento.error}</p>}
        {entrenamiento.resultados?.length > 0 && <ul>
          {entrenamiento.resultados.map((resultado) => <li key={resultado.franjaHorariaId}>
            <strong>{resultado.franja} · {resultadoTexto[resultado.estado] ?? resultado.estado}</strong>
            {resultado.mae === null
              ? <span>Observaciones: {resultado.observacionesEntrenamiento}/56 de entrenamiento y {resultado.observacionesValidacion}/14 de validación.</span>
              : <span>MAE {numero(resultado.mae, 2)} · RMSE {numero(resultado.rmse, 2)} · referencia {numero(resultado.maeReferencia, 2)}</span>}
          </li>)}
        </ul>}
      </div>}
      {modelos === null && !errorModelos && <p role="status">Cargando versiones…</p>}
      {modelos?.length === 0 && <p>No hay modelos SSA entrenados. El modelo base solo genera predicciones de baja confianza cuando existen observaciones comparables.</p>}
      {modelos?.length > 0 && <ul className="dashboard-modelos-lista">
        {modelos.map((modelo) => <li key={modelo.id}>
          <div>
            <strong>{modelo.franja} · {modelo.version}{modelo.activo ? ' · Activo' : ''}</strong>
            <span>MAE {numero(modelo.mae, 2)} · RMSE {numero(modelo.rmse, 2)} · Entrenado {fechaHora(modelo.creadoEn)}</span>
            <small>Validación: {modelo.validacionDesde} a {modelo.validacionHasta}</small>
          </div>
        </li>)}
      </ul>}
      {anteriores.length > 0 && <div className="dashboard-modelos-rollback">
        <label>Motivo del rollback
          <input type="text" maxLength={500} value={motivoRollback}
            onChange={(evento) => setMotivoRollback(evento.target.value)} />
        </label>
        <ul className="dashboard-modelos-lista">
          {anteriores.map((modelo) => <li key={modelo.id}>
            <div><strong>Versión anterior · {modelo.franja} · {modelo.version}</strong>
              <span>MAE {numero(modelo.mae, 2)} · creada {fechaHora(modelo.creadoEn)}</span></div>
            <button type="button" className="button button-secondary button-small"
              disabled={!motivoRollback.trim() || activando !== null}
              onClick={() => activarModelo(modelo)}>
              {activando === modelo.id ? 'Activando…' : 'Reactivar versión'}
            </button>
          </li>)}
        </ul>
      </div>}
    </section>
  );
}
