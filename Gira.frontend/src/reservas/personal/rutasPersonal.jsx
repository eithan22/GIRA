import { Component, lazy, Suspense } from 'react';

// Carga diferida de las pantallas del personal (GI-398). App.jsx solo tiene que importar estos cuatro
// componentes en lugar de las páginas: cada uno ya trae su Suspense y su manejador de errores, así que no
// hace falta tocar ninguna <Route>. Mientras App.jsx no los use, este archivo no entra en el paquete y todo
// sigue funcionando con los imports estáticos de siempre.
//
// Las cuatro pantallas salen de un único módulo (paginasPersonal.jsx): una sola importación dinámica
// genera un solo chunk. El CSS de Eithan lo importa PaginaPersonal desde JS, así que se comparte con las
// pantallas públicas y no se duplica aquí.

const cargarPaginas = () => import('./paginasPersonal.jsx');

const Agenda = lazy(() => cargarPaginas().then((modulo) => ({ default: modulo.AgendaPersonal })));
const Mapa = lazy(() => cargarPaginas().then((modulo) => ({ default: modulo.MapaPersonal })));
const Visitas = lazy(() => cargarPaginas().then((modulo) => ({ default: modulo.VisitasPersonal })));
const Configuracion = lazy(() => cargarPaginas().then((modulo) => ({ default: modulo.ConfiguracionPersonal })));

// Estilos en línea a propósito: mientras llega el chunk, el CSS del personal todavía no está cargado.
const ESTILO_AVISO = { padding: '32px 0', color: '#4b5669', fontSize: 15, lineHeight: 1.6 };

function CargandoPantalla() {
  return <div role="status" aria-live="polite" style={ESTILO_AVISO}>Cargando la pantalla…</div>;
}

/**
 * Manejador de errores de la carga. El front no tenía ninguno: si falla la descarga del chunk (sin red, o
 * una pestaña vieja tras un despliegue nuevo) la pantalla quedaría en blanco. Aquí se muestra un mensaje
 * y un botón para recargar. No registra nada en consola ni en storage.
 */
class LimiteErroresCarga extends Component {
  constructor(props) {
    super(props);
    this.state = { fallo: false };
  }

  static getDerivedStateFromError() {
    return { fallo: true };
  }

  render() {
    if (!this.state.fallo) return this.props.children;
    return (
      <div role="alert" style={ESTILO_AVISO}>
        <p style={{ margin: '0 0 12px' }}>No pudimos cargar esta pantalla. Revisa tu conexión e inténtalo de nuevo.</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          style={{ minHeight: 44, padding: '0 20px', fontWeight: 700, color: '#fff', background: '#5948d3', border: 'none', borderRadius: 12, cursor: 'pointer' }}
        >Recargar</button>
      </div>
    );
  }
}

function conCarga(Pagina) {
  return function PaginaDiferida() {
    return (
      <LimiteErroresCarga>
        <Suspense fallback={<CargandoPantalla />}>
          <Pagina />
        </Suspense>
      </LimiteErroresCarga>
    );
  };
}

export const AgendaPersonal = conCarga(Agenda);
export const MapaPersonal = conCarga(Mapa);
export const VisitasPersonal = conCarga(Visitas);
export const ConfiguracionPersonal = conCarga(Configuracion);
