import { useEffect, useMemo, useState } from 'react'
import { getHistory, getKpis, getPrediction } from './services/analiticaService.js'
import './App.css'

const DAY_MS = 24 * 60 * 60 * 1000
const numberFormat = new Intl.NumberFormat('es-DO')

function localDateString(date) {
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return localDate.toISOString().slice(0, 10)
}

function formatDate(date) {
  return new Intl.DateTimeFormat('es-DO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date)
}

function parseLocalDate(value) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function formatTime(value) {
  return new Intl.DateTimeFormat('es-DO', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function hourLabel(hour) {
  return `${String(hour).padStart(2, '0')}:00`
}

async function fetchDashboardData(date, today, signal) {
  const isPastOrToday = date <= today
  const historyEnd = isPastOrToday ? date : localDateString(new Date(Date.now() - DAY_MS))
  const historyStart = isPastOrToday
    ? date
    : localDateString(new Date(new Date(`${historyEnd}T00:00:00`).getTime() - 6 * DAY_MS))
  const [kpis, predictions, history] = await Promise.all([
    getKpis(signal),
    getPrediction(date, signal),
    getHistory(historyStart, historyEnd, signal),
  ])

  return { kpis, predictions, history }
}

function Icon({ name, size = 20 }) {
  const paths = {
    chart: <><rect x="3" y="3" width="18" height="18" rx="4" /><path d="M7 15v-3m5 3V8m5 7v-5" /></>,
    calendar: <><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M16 3v4M8 3v4M3 10h18" /></>,
    users: <><path d="M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" /><circle cx="10" cy="7" r="4" /><path d="M20 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" /></>,
    trend: <><path d="m3 17 6-6 4 4 8-8" /><path d="M15 7h6v6" /></>,
    clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
    refresh: <><path d="M20 7v5h-5M4 17v-5h5" /><path d="M5.6 9A7 7 0 0 1 18 6l2 1m-16 10 2 1a7 7 0 0 0 12.4-3" /></>,
    chevron: <path d="m9 18 6-6-6-6" />,
    down: <path d="m7 10 5 5 5-5" />,
    sparkle: <><path d="m12 3 1.9 5.8L20 11l-6.1 2.2L12 19l-2-5.8L4 11l6-2.2L12 3Z" /><path d="m19 14 1.2 2.8L23 18l-2.8 1.2L19 22l-1.2-2.8L15 18l2.8-1.2L19 14Z" /></>,
  }

  return (
    <svg
      aria-hidden="true"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths[name]}
    </svg>
  )
}

function SideNavigation() {
  return (
    <aside className="sidebar">
      <a className="brand" href="/" aria-label="GIRA inicio">
        <span className="brand-mark">G</span>
        <span className="brand-name">GIRA<span>.</span></span>
      </a>

      <div className="workspace-label">RESTAURANTE</div>
      <button className="restaurant-switcher" type="button">
        <span className="restaurant-avatar">M</span>
        <span className="restaurant-name">Mi restaurante<small>Sucursal principal</small></span>
        <Icon name="down" size={16} />
      </button>

      <nav className="side-nav" aria-label="Navegación principal">
        <div className="nav-heading">MENÚ PRINCIPAL</div>
        <a className="nav-link active" href="#dashboard" aria-current="page">
          <Icon name="chart" /><span>Dashboard</span>
        </a>
        <a className="nav-link disabled" href="#reservas" aria-disabled="true">
          <Icon name="calendar" /><span>Reservas</span><span className="nav-soon">Pronto</span>
        </a>
        <a className="nav-link disabled" href="#personal" aria-disabled="true">
          <Icon name="users" /><span>Personal</span><span className="nav-soon">Pronto</span>
        </a>
      </nav>

      <div className="sidebar-bottom">
        <div className="plan-card">
          <span className="plan-icon"><Icon name="sparkle" size={17} /></span>
          <strong>Analítica inteligente</strong>
          <p>Decisiones más claras, con datos reales de tu restaurante.</p>
        </div>
        <div className="user-profile">
          <span className="profile-avatar">GE</span>
          <span className="profile-copy"><strong>Usuario gerencial</strong><small>Panel gerencial</small></span>
          <button className="profile-menu" type="button" aria-label="Opciones de perfil">
            <span /><span /><span />
          </button>
        </div>
      </div>
    </aside>
  )
}

function KpiCard({ icon, label, value, detail, tone, trend }) {
  return (
    <article className="kpi-card">
      <div className="kpi-top">
        <span className={`kpi-icon ${tone}`}><Icon name={icon} size={19} /></span>
        {trend != null && (
          <span className={`trend-pill ${trend >= 0 ? 'positive' : 'negative'}`}>
            <Icon name="trend" size={13} /> {Math.abs(trend)}%
          </span>
        )}
      </div>
      <p className="kpi-label">{label}</p>
      <strong className="kpi-value">{value}</strong>
      <p className="kpi-detail">{detail}</p>
    </article>
  )
}

function FlowChart({ history, predictions, loading }) {
  const chart = useMemo(() => {
    const actualByHour = Array.from({ length: 24 }, () => 0)
    const predictionByHour = Array.from({ length: 24 }, () => 0)
    const historyDays = new Set(history.map((record) => record.fecha)).size

    for (const record of history) {
      const hour = Number(record.horaInicio.slice(0, 2))
      if (Number.isInteger(hour) && hour >= 0 && hour < 24) {
        actualByHour[hour] += record.cantidadComensales
      }
    }

    if (historyDays > 1) {
      for (let hour = 0; hour < actualByHour.length; hour++) {
        actualByHour[hour] /= historyDays
      }
    }

    for (const record of predictions) {
      const hour = Number(record.horaInicio.slice(0, 2))
      if (Number.isInteger(hour) && hour >= 0 && hour < 24) {
        predictionByHour[hour] += record.afluenciaEstimada
      }
    }

    const maximum = Math.max(1, ...actualByHour, ...predictionByHour)
    const chartWidth = 720
    const chartHeight = 230
    const left = 44
    const right = 10
    const top = 18
    const bottom = 30
    const innerWidth = chartWidth - left - right
    const innerHeight = chartHeight - top - bottom
    const x = (index) => left + (index / 23) * innerWidth
    const y = (value) => top + innerHeight - (value / maximum) * innerHeight
    const actualPoints = actualByHour.map((value, index) => `${x(index)},${y(value)}`).join(' ')
    const predictionPoints = predictionByHour.map((value, index) => `${x(index)},${y(value)}`).join(' ')

    return { actualByHour, predictionByHour, maximum, chartWidth, chartHeight, left, right, top, bottom, innerWidth, innerHeight, x, y, actualPoints, predictionPoints }
  }, [history, predictions])

  const hasHistory = history.length > 0
  const hasPredictions = predictions.length > 0

  if (loading) {
    return <div className="chart-message"><span className="spinner" /> Cargando datos de afluencia…</div>
  }

  if (!hasHistory && !hasPredictions) {
    return (
      <div className="chart-empty">
        <span className="empty-icon"><Icon name="chart" size={23} /></span>
        <strong>Aún no hay datos para esta fecha</strong>
        <span>Las predicciones aparecerán al disponer de al menos 14 días de historial.</span>
      </div>
    )
  }

  return (
    <div className="chart-wrap">
      <div className="chart-legend">
        <span><i className="legend-dot actual" /> Afluencia registrada</span>
        <span><i className="legend-dot forecast" /> Predicción SSA</span>
      </div>
      <div className="chart-scroll">
        <svg
          className="flow-chart"
          viewBox={`0 0 ${chart.chartWidth} ${chart.chartHeight}`}
          role="img"
          aria-label="Gráfica de afluencia por hora, con valores históricos y predichos"
        >
          {[0, 0.25, 0.5, 0.75, 1].map((fraction) => {
            const value = Math.round(chart.maximum * fraction)
            const y = chart.top + chart.innerHeight * (1 - fraction)
            return (
              <g key={fraction}>
                <line x1={chart.left} x2={chart.chartWidth - chart.right} y1={y} y2={y} className="grid-line" />
                <text x={chart.left - 10} y={y + 4} textAnchor="end" className="axis-label">{value}</text>
              </g>
            )
          })}
          <path d={`M ${chart.left} ${chart.top + chart.innerHeight} L ${chart.chartWidth - chart.right} ${chart.top + chart.innerHeight}`} className="axis-line" />
          {Array.from({ length: 7 }, (_, index) => index * 4).map((hour) => (
            <text key={hour} x={chart.x(hour)} y={chart.chartHeight - 7} textAnchor="middle" className="axis-label">
              {hourLabel(hour)}
            </text>
          ))}
          {hasHistory && <polyline points={chart.actualPoints} className="chart-line history-line" />}
          {hasPredictions && <polyline points={chart.predictionPoints} className="chart-line forecast-line" />}
          {hasHistory && chart.actualByHour.map((value, index) => value > 0 && (
            <circle key={`actual-${index}`} cx={chart.x(index)} cy={chart.y(value)} r="3.5" className="chart-point actual-point">
              <title>{hourLabel(index)} · Real: {numberFormat.format(value)}</title>
            </circle>
          ))}
          {hasPredictions && chart.predictionByHour.map((value, index) => value > 0 && (
            <circle key={`forecast-${index}`} cx={chart.x(index)} cy={chart.y(value)} r="3.5" className="chart-point forecast-point">
              <title>{hourLabel(index)} · Predicción: {numberFormat.format(value)}</title>
            </circle>
          ))}
        </svg>
      </div>
    </div>
  )
}

function App() {
  const [selectedDate, setSelectedDate] = useState(() => localDateString(new Date(Date.now() + DAY_MS)))
  const [dashboard, setDashboard] = useState({ date: '', kpis: null, history: [], predictions: [] })
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState('')
  const [todayDate] = useState(() => new Date())
  const today = localDateString(todayDate)
  const { kpis, history, predictions } = dashboard
  const loading = dashboard.date !== selectedDate && !error

  useEffect(() => {
    const controller = new AbortController()

    fetchDashboardData(selectedDate, today, controller.signal)
      .then((data) => {
        setDashboard({ date: selectedDate, ...data })
        setError('')
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') {
          setError(requestError.message)
        }
      })

    return () => controller.abort()
  }, [selectedDate, today])

  const refresh = async () => {
    setRefreshing(true)
    try {
      const data = await fetchDashboardData(selectedDate, today)
      setDashboard({ date: selectedDate, ...data })
      setError('')
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setRefreshing(false)
    }
  }

  const shownDate = parseLocalDate(selectedDate)
  const predictedText = kpis?.prediccionManana == null
    ? '—'
    : numberFormat.format(kpis.prediccionManana)
  const trend = kpis?.variacionPorcentual ?? null

  return (
    <div className="app-shell">
      <SideNavigation />
      <main className="main-content" id="dashboard">
        <header className="topbar">
          <div className="breadcrumbs"><span>GIRA</span><Icon name="chevron" size={15} /><strong>Dashboard gerencial</strong></div>
          <div className="topbar-right">
            <span className="live-indicator"><i /> API de Analítica</span>
            <span className="topbar-divider" />
            <span className="today-label"><Icon name="calendar" size={16} /> {formatDate(todayDate)}</span>
            <span className="small-avatar">GE</span>
          </div>
        </header>

        <div className="page-content">
          <section className="page-heading">
            <div>
              <div className="eyebrow">CENTRO DE CONTROL</div>
              <h1>Dashboard gerencial</h1>
              <p>Una visión clara del comportamiento y la afluencia de tu restaurante.</p>
            </div>
            <div className="heading-actions">
              <label className="date-filter">
                <Icon name="calendar" size={17} />
                <span className="sr-only">Fecha del gráfico</span>
                <input
                  type="date"
                  value={selectedDate}
                  onChange={(event) => {
                    setError('')
                    setSelectedDate(event.target.value)
                  }}
                />
                <Icon name="down" size={14} />
              </label>
              <button className="refresh-button" type="button" onClick={refresh} disabled={refreshing}>
                <span className={refreshing ? 'refresh-spin' : ''}><Icon name="refresh" size={17} /></span>
                <span>{refreshing ? 'Actualizando' : 'Actualizar'}</span>
              </button>
            </div>
          </section>

          {error && (
            <div className="error-banner" role="alert">
              <span className="error-symbol">!</span>
              <span><strong>No se pudieron cargar los datos.</strong> {error}</span>
              <button type="button" onClick={refresh}>Reintentar</button>
            </div>
          )}

          <section className="kpi-grid" aria-label="Indicadores clave">
            <KpiCard
              icon="users"
              label="Comensales · últimos 30 días"
              value={loading || !kpis ? '—' : numberFormat.format(kpis.comensalesUltimos30Dias)}
              detail={kpis ? `${numberFormat.format(kpis.diasConActividad)} días con actividad` : 'Datos no disponibles'}
              tone="violet"
              trend={trend}
            />
            <KpiCard
              icon="chart"
              label="Promedio diario"
              value={loading || !kpis ? '—' : numberFormat.format(kpis.promedioDiario)}
              detail="Comensales por día · últimos 30 días"
              tone="blue"
            />
            <KpiCard
              icon="sparkle"
              label="Afluencia estimada · mañana"
              value={loading ? '—' : predictedText}
              detail={!kpis ? 'Datos no disponibles' : kpis.prediccionManana == null ? 'Se necesitan 14 días de historial' : 'Pronóstico del modelo ML.NET SSA'}
              tone="amber"
            />
            <KpiCard
              icon="clock"
              label="Fecha del pronóstico"
              value={formatDate(shownDate).split(' de ')[0]}
              detail={formatDate(shownDate)}
              tone="green"
            />
          </section>

          <section className="content-grid">
            <article className="panel flow-panel">
              <div className="panel-heading">
                <div>
                  <div className="panel-title-row"><h2>Afluencia por hora</h2><span className="model-tag"><Icon name="sparkle" size={13} /> SSA</span></div>
                  <p>Historial reciente y pronóstico horario para {formatDate(shownDate)}.</p>
                </div>
                <button className="period-control" type="button" aria-label="Intervalo por hora">
                  <Icon name="clock" size={15} /> Por hora <Icon name="down" size={13} />
                </button>
              </div>
              <FlowChart history={history} predictions={predictions} loading={loading} />
              <div className="chart-footnote">
                <span className="footnote-dot" />
                Las predicciones se actualizan diariamente con historial suficiente.
              </div>
            </article>

            <aside className="panel forecast-panel">
              <div className="panel-heading forecast-heading">
                <div>
                  <div className="panel-title-row"><h2>Predicción del día</h2></div>
                  <p>{formatDate(shownDate)}</p>
                </div>
                <span className="forecast-icon"><Icon name="sparkle" size={18} /></span>
              </div>
              {loading ? (
                <div className="forecast-empty"><span className="spinner" /></div>
              ) : predictions.length > 0 ? (
                <>
                  <div className="forecast-total">
                    <strong>{numberFormat.format(predictions.reduce((total, row) => total + row.afluenciaEstimada, 0))}</strong>
                    <span>comensales estimados</span>
                  </div>
                  <div className="peak-hour">
                    <span className="peak-icon"><Icon name="trend" size={17} /></span>
                    <span><small>Hora de mayor afluencia</small><strong>{predictions.reduce((peak, row) => row.afluenciaEstimada > peak.afluenciaEstimada ? row : peak).horaInicio.slice(0, 5)}</strong></span>
                  </div>
                  <div className="forecast-caption">Modelo <b>ML.NET · SSA</b><span>Actualizado {formatTime(predictions[0].fechaGeneracion)}</span></div>
                </>
              ) : (
                <div className="forecast-no-data">
                  <span className="forecast-no-data-icon"><Icon name="sparkle" size={20} /></span>
                  <strong>Sin predicción disponible</strong>
                  <span>El modelo necesita al menos 14 días de historial para calcular una estimación.</span>
                </div>
              )}
            </aside>
          </section>

          <footer className="page-footer">
            <span>GIRA · Analítica e Inteligencia Artificial</span>
            <span>Los indicadores se calculan a partir de las reservas confirmadas.</span>
          </footer>
        </div>
      </main>
    </div>
  )
}

export default App
