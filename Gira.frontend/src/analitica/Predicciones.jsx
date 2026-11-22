import { useEffect, useState } from 'react';
import { fecha, fechaHora, numero, sumarDias } from '../inventario/inventarioFormato.js';
import { analiticaService } from './analiticaService.js';
import { compararPredicciones, mensajeErrorAnalitica, validarFechaPrediccion, validarPredicciones } from './dashboardDatos.js';

export default function Predicciones({ hoy, fechaInicial, recarga }) {
  const [formulario, setFormulario] = useState(fechaInicial);
  const [seleccion, setSeleccion] = useState(formulario);
  const [franjas, setFranjas] = useState([]);
  const [franjaSeleccionada, setFranjaSeleccionada] = useState('');
  const [revision, setRevision] = useState(0);
  const [errorFecha, setErrorFecha] = useState('');
  const [estado, setEstado] = useState({
    fecha: null, franjaId: '', predicciones: null, historial: null, errorPrediccion: '', errorHistorial: '',
  });

  useEffect(() => {
    let activo = true;
    setEstado({
      fecha: seleccion,
      franjaId: franjaSeleccionada,
      predicciones: null,
      historial: null,
      errorPrediccion: '',
      errorHistorial: '',
    });
    const periodo = { desde: seleccion, hasta: seleccion };
    analiticaService.prediccion(periodo, franjaSeleccionada || undefined)
      .then((respuesta) => validarPredicciones(respuesta, seleccion))
      .then((predicciones) => {
        if (!activo) return;
        if (!franjaSeleccionada) {
          setFranjas(predicciones.map((item) => ({
            id: item.franjaHorariaId,
            nombre: item.franja,
          })));
        }
        setEstado((anterior) => ({ ...anterior, predicciones }));
      })
      .catch((error) => { if (activo) setEstado((anterior) => ({ ...anterior, errorPrediccion: mensajeErrorAnalitica(error, 'las predicciones') })); });
    analiticaService.historial(periodo)
      .then((historial) => {
        if (!Array.isArray(historial)) throw new Error('Respuesta de historial inválida');
        if (activo) setEstado((anterior) => ({ ...anterior, historial }));
      })
      .catch((error) => { if (activo) setEstado((anterior) => ({ ...anterior, errorHistorial: mensajeErrorAnalitica(error, 'la afluencia real') })); });
    return () => { activo = false; };
  }, [seleccion, franjaSeleccionada, recarga, revision]);

  function aplicar(evento) {
    evento.preventDefault();
    if (!validarFechaPrediccion(formulario, hoy)) {
      setErrorFecha('Elige una fecha entre los últimos 12 meses y los próximos 14 días.');
      return;
    }
    setErrorFecha('');
    if (formulario !== seleccion) {
      setFranjas([]);
      setFranjaSeleccionada('');
    }
    setSeleccion(formulario);
    if (formulario === seleccion) setRevision((valor) => valor + 1);
  }

  const vigente = estado.fecha === seleccion && estado.franjaId === franjaSeleccionada && !errorFecha;
  const filas = vigente && estado.predicciones && estado.historial
    ? compararPredicciones(estado.predicciones, estado.historial)
    : vigente && estado.predicciones?.map((item) => ({ ...item, real: null }));
  const maximo = Math.max(1, ...(filas || []).flatMap((fila) => [fila.afluenciaEstimada, fila.real ?? 0]));

  return (
    <section className="panel dashboard-predicciones">
      <h2>Predicción y afluencia real</h2>
      <p>La comparación comienza en la última fecha del período seleccionado; también puedes consultar otra fecha.</p>
      <p>Cuando la generación automática está habilitada, se ejecuta después del cierre y requiere observaciones reales para generar pronósticos.</p>
      <form className="dashboard-fecha-prediccion" onSubmit={aplicar} noValidate>
        <label>Fecha de predicción o comparación
          <input type="date" required min={sumarDias(hoy, -365)} max={sumarDias(hoy, 14)} value={formulario}
            onChange={(evento) => setFormulario(evento.target.value)} />
        </label>
        <label>Franja horaria
          <select value={franjaSeleccionada} disabled={franjas.length === 0}
            onChange={(evento) => setFranjaSeleccionada(evento.target.value)}>
            <option value="">Todas las franjas</option>
            {franjas.map((franja) => <option key={franja.id} value={franja.id}>{franja.nombre}</option>)}
          </select>
        </label>
        <button type="submit" className="button button-primary">Consultar predicción</button>
      </form>
      {errorFecha && <p className="notice-error" role="alert">{errorFecha}</p>}
      {vigente && <p>Fecha consultada: {fecha(seleccion)}</p>}
      {vigente && estado.errorPrediccion && <p className="notice-error" role="alert">{estado.errorPrediccion}</p>}
      {vigente && estado.errorHistorial && <p className="notice-error" role="alert">{estado.errorHistorial}</p>}
      {vigente && (estado.errorPrediccion || estado.errorHistorial) &&
        <button type="button" className="button button-secondary button-small" onClick={() => setRevision((valor) => valor + 1)}>Reintentar predicción</button>}
      {vigente && !estado.errorPrediccion && !estado.predicciones && <p role="status">Cargando predicción…</p>}
      {vigente && estado.predicciones?.length === 0 && <p>No hay predicciones disponibles para esta fecha.</p>}
      {filas?.length > 0 && (
        <ul className="dashboard-prediccion-lista">
          {filas.map((fila) => (
            <li key={fila.franjaHorariaId}>
              <strong>{fila.franja}</strong>
              <span>{fila.bajaConfianza ? 'Baja confianza' : 'Confianza normal'} · Modelo {fila.versionModelo} · Generada {fechaHora(fila.generadaEn)}</span>
              <span>Estimación: {numero(fila.afluenciaEstimada, 0)} comensales</span>
              <div className="dashboard-barra" aria-hidden="true"><span style={{ width: `${fila.afluenciaEstimada / maximo * 100}%` }} /></div>
              <span>{fila.limiteInferior === null ? 'Intervalo de confianza no disponible' : `Intervalo de confianza: ${numero(fila.limiteInferior, 2)}–${numero(fila.limiteSuperior, 2)} comensales`}</span>
              <span>Afluencia real: {estado.errorHistorial ? 'no disponible' : !estado.historial ? 'cargando' : fila.real === null ? 'sin registros' : `${numero(fila.real, 0)} comensales`}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
