import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import Icon from '../security/components/Icon.jsx';
import { inventarioService } from '../inventario/inventarioService.js';
import { TIPOS_ALERTA, dinero, fecha, hoyLocal, numero, sumarDias } from '../inventario/inventarioFormato.js';
import { analiticaService } from './analiticaService.js';
import { diasGrafico, mensajeErrorAnalitica, validarKpis } from './dashboardDatos.js';
import Predicciones from './Predicciones.jsx';
import Recomendaciones from './Recomendaciones.jsx';
import ModelosAnalitica from './ModelosAnalitica.jsx';
import CalendarioFeriados from './CalendarioFeriados.jsx';
import './dashboard.css';

function Tarjeta({ titulo, valor, detalle, icono, tono = 'violet' }) {
  return (
    <article className="dashboard-stat">
      <div className={`stat-symbol ${tono}`}><Icon name={icono} size={19} /></div>
      <div><span>{titulo}</span><strong>{valor}</strong><small>{detalle}</small></div>
    </article>
  );
}

const porcentaje = (valor) => valor === null ? '—' : `${numero(valor, 1)} %`;
const diasSemana = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
const estadoVacio = { clave: '', cargandoHistorial: true, cargandoAlertas: true, cargandoKpis: true, historial: null, alertas: null, kpis: null, errorHistorial: '', errorAlertas: '', errorKpis: '' };

export default function Dashboard() {
  const hoy = hoyLocal();
  const [formulario, setFormulario] = useState(() => ({ desde: sumarDias(hoy, -13), hasta: hoy }));
  const [periodo, setPeriodo] = useState(formulario);
  const [recarga, setRecarga] = useState(0);
  const [resultado, setEstado] = useState(estadoVacio);
  const [errorRango, setErrorRango] = useState('');
  const clave = `${periodo.desde}|${periodo.hasta}|${recarga}`;
  const estado = resultado.clave === clave ? resultado : estadoVacio;

  useEffect(() => { document.title = 'Dashboard gerencial · GIRA'; }, []);
  useEffect(() => {
    let activo = true;
    setEstado({ ...estadoVacio, clave });
    analiticaService.historial(periodo)
      .then((historial) => { if (activo) setEstado((anterior) => ({ ...anterior, historial, cargandoHistorial: false })); })
      .catch((error) => { if (activo) setEstado((anterior) => ({ ...anterior, errorHistorial: mensajeErrorAnalitica(error, 'el historial de afluencia'), cargandoHistorial: false })); });
    inventarioService.listarAlertas({ estado: 'Abierta', page: 1, pageSize: 5 })
      .then((alertas) => { if (activo) setEstado((anterior) => ({ ...anterior, alertas, cargandoAlertas: false })); })
      .catch((error) => { if (activo) setEstado((anterior) => ({ ...anterior, errorAlertas: mensajeErrorAnalitica(error, 'las alertas de inventario'), cargandoAlertas: false })); });
    analiticaService.kpis(periodo)
      .then((respuesta) => validarKpis(respuesta, periodo))
      .then((kpis) => { if (activo) setEstado((anterior) => ({ ...anterior, kpis, cargandoKpis: false })); })
      .catch((error) => { if (activo) setEstado((anterior) => ({ ...anterior, errorKpis: mensajeErrorAnalitica(error, 'los indicadores'), cargandoKpis: false })); });
    return () => { activo = false; };
  }, [periodo, clave]);

  const dias = useMemo(() => estado.historial ? diasGrafico(estado.historial, periodo.desde, periodo.hasta) : [], [estado.historial, periodo]);
  const maximo = Math.max(1, ...dias.map((dia) => dia.total ?? 0));
  const valorKpi = (valor, formatear = (v) => numero(v, 0)) =>
    estado.cargandoKpis ? '…' : estado.kpis ? formatear(valor) : '—';
  const detallePrecision = estado.cargandoKpis ? 'Consultando predicciones verificables'
    : !estado.kpis ? 'Indicador no disponible'
      : estado.kpis.precisionModelo === null ? 'Sin predicciones verificables en el período'
        : 'Error absoluto medio de predicciones ya cumplidas';

  function aplicar(evento) {
    evento.preventDefault();
    if (!formulario.desde || !formulario.hasta || formulario.desde > formulario.hasta || formulario.hasta > hoy) {
      setErrorRango('Selecciona un intervalo válido que no incluya fechas futuras.');
      return;
    }
    setErrorRango('');
    setPeriodo({ ...formulario });
  }

  return (
    <section className="page-section dashboard enter">
      <div className="page-heading-row">
        <div><span className="eyebrow">ANALÍTICA · GIRA</span><h1>Dashboard gerencial</h1><p>Una vista de la afluencia y las alertas que requieren atención.</p></div>
        <button type="button" className="button button-secondary" onClick={() => setRecarga((valor) => valor + 1)} disabled={estado.cargandoHistorial || estado.cargandoAlertas || estado.cargandoKpis}>Actualizar</button>
      </div>
      <form className="dashboard-filtros panel" onSubmit={aplicar}>
        <div><strong>Período de análisis</strong><small>La afluencia corresponde a las fechas seleccionadas.</small></div>
        <label>Desde <input type="date" required max={hoy} value={formulario.desde} onChange={(e) => setFormulario({ ...formulario, desde: e.target.value })} /></label>
        <label>Hasta <input type="date" required max={hoy} value={formulario.hasta} onChange={(e) => setFormulario({ ...formulario, hasta: e.target.value })} /></label>
        <button type="submit" className="button button-primary">Aplicar</button>
      </form>
      {errorRango && <div className="notice-error" role="alert">{errorRango}</div>}
      {!errorRango && <>
      {(estado.errorHistorial || estado.errorAlertas || estado.errorKpis) && (
        <div className="notice-error" role="alert">
          {[estado.errorHistorial, estado.errorAlertas, estado.errorKpis].filter(Boolean).join(' ')}
          <button type="button" className="button button-secondary button-small" onClick={() => setRecarga((valor) => valor + 1)}>Reintentar</button>
        </div>
      )}
      <p className="dashboard-nota">La afluencia muestra los eventos registrados desde que se active Analítica; el historial anterior y la recuperación de eventos pendientes todavía no están disponibles.</p>
      <p className="dashboard-nota">Período mostrado: {fecha(periodo.desde)} al {fecha(periodo.hasta)}.</p>
      <p className="dashboard-nota">Los indicadores corresponden al período seleccionado, salvo las alertas abiertas, que son actuales. Para comparar con los períodos del SRS, selecciona un día, semana o mes según el indicador.</p>
      <div className="dashboard-stats" aria-live="polite">
        <Tarjeta titulo="Comensales registrados" valor={valorKpi(estado.kpis?.comensalesAtendidos)} detalle="Con y sin reserva" icono="users" />
        <Tarjeta titulo="Tasa de ocupación" valor={valorKpi(estado.kpis?.tasaOcupacion, porcentaje)} detalle="Requiere snapshots diarios; no se reconstruyen fechas anteriores" icono="users" />
        <Tarjeta titulo="No asistieron" valor={valorKpi(estado.kpis?.tasaNoShow, porcentaje)} detalle="De las reservas resueltas · semana o mes" icono="calendar" tono="amber" />
        <Tarjeta titulo="Cancelación" valor={valorKpi(estado.kpis?.tasaCancelacion, porcentaje)} detalle="Reservas creadas en el período y canceladas a la fecha · semana o mes" icono="calendar" tono="amber" />
        <Tarjeta titulo="Sin reserva" valor={valorKpi(estado.kpis?.proporcionWalkIns, porcentaje)} detalle="De los comensales atendidos · semana o mes" icono="users" tono="green" />
        <Tarjeta titulo="Precisión del modelo" valor={valorKpi(estado.kpis?.precisionModelo)} detalle={detallePrecision} icono="calendar" />
        <Tarjeta titulo="Costo por comensal" valor={valorKpi(estado.kpis?.costoInsumosPorComensal, dinero)} detalle="No disponible si hay salidas o mermas sin costo registrado" icono="users" />
        <Tarjeta titulo="Merma" valor={valorKpi(estado.kpis?.merma, porcentaje)} detalle="Requiere costos completos de salidas y mermas · mes" icono="alert" tono="amber" />
        <Tarjeta titulo="Alertas abiertas" valor={valorKpi(estado.kpis?.alertasAbiertas)} detalle="Inventario · estado actual" icono="alert" tono="amber" />
        <Tarjeta titulo="Horas de personal por comensal" valor={valorKpi(estado.kpis?.horasPersonalPorComensal, (v) => v === null || v === undefined ? '—' : `${numero(v, 2)} h`)} detalle="Se calcula con turnos que tengan entrada y salida registradas" icono="users" />
      </div>
      <section className="panel dashboard-promedio">
        <h2>Afluencia promedio por día y franja</h2>
        <p>Período seleccionado · usa solo fechas con registros; no interpreta las fechas sin registros como cero. Los nombres de franjas son los actuales.</p>
        {estado.cargandoKpis ? <p>Cargando promedio…</p>
          : !estado.kpis ? <p>No se pudo consultar el promedio.</p>
            : estado.kpis.afluenciaPromedio.length === 0 ? <p>No hay fechas observadas en el período.</p>
              : <ul>{estado.kpis.afluenciaPromedio.map((item) => (
                <li key={`${item.diaSemana}-${item.franjaHorariaId}`}>
                  <strong>{diasSemana[item.diaSemana]} · {item.franja}</strong>
                  <span>{numero(item.promedio, 1)} comensales · {item.fechasObservadas} fecha(s) observada(s)</span>
                </li>
              ))}</ul>}
      </section>
      <div className="dashboard-contenido">
        <section className="panel dashboard-panel">
          <div className="panel-heading"><div><h2>Afluencia real</h2><p>Últimos 14 días del período · comensales por día</p></div><span className="count-badge">REAL</span></div>
          {estado.cargandoHistorial ? <p className="dashboard-mensaje">Cargando afluencia…</p>
            : !estado.historial ? <p className="dashboard-mensaje">No se puede mostrar el gráfico sin el historial.</p>
              : dias.every((dia) => dia.total === null) ? <p className="dashboard-mensaje">No hay registros de afluencia en las fechas mostradas.</p>
              : <ol className="dashboard-barras" aria-label="Comensales atendidos por día">
                {dias.map((dia) => (
                  <li key={dia.fecha}>
                    <span>{fecha(dia.fecha).slice(0, 5)}</span>
                    <div className="dashboard-barra">{dia.total !== null && <span style={{ width: `${dia.total / maximo * 100}%` }} />}</div>
                    <strong>{dia.total === null ? 'Sin datos' : numero(dia.total, 0)}</strong>
                  </li>
                ))}
              </ol>}
          <p className="dashboard-pendiente">La comparación por fecha y franja se presenta en la sección de predicciones cuando hay datos disponibles.</p>
        </section>
        <section className="panel dashboard-panel">
          <div className="panel-heading"><div><h2>Alertas de inventario</h2><p>Las más recientes que siguen abiertas</p></div><Link className="dashboard-enlace" to="/inventario/alertas">Ver todas <Icon name="arrow" size={14} /></Link></div>
          {estado.cargandoAlertas ? <p className="dashboard-mensaje">Cargando alertas…</p>
            : !estado.alertas ? <p className="dashboard-mensaje">No se pueden mostrar las alertas.</p>
              : estado.alertas.items.length === 0 ? <p className="dashboard-mensaje">No hay alertas abiertas.</p>
                : <ul className="dashboard-alertas">{estado.alertas.items.map((alerta) => (
                  <li key={alerta.id}><span className="dashboard-alerta-icono"><Icon name="alert" size={16} /></span><div><strong>{TIPOS_ALERTA[alerta.tipo] ?? alerta.tipo}</strong><p>{alerta.insumo}</p></div></li>
                ))}</ul>}
        </section>
      </div>
      </>}
      <Predicciones key={`${periodo.desde}|${periodo.hasta}`} hoy={hoy}
        fechaInicial={periodo.hasta} recarga={recarga} />
      <Recomendaciones recarga={recarga} />
      <ModelosAnalitica recarga={recarga} />
      <CalendarioFeriados hoy={hoy} recarga={recarga} />
    </section>
  );
}
