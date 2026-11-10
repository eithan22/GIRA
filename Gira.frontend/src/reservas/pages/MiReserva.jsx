import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { reservasService, extraerMensajeError } from '../reservasService.js';
import { MENSAJE_ERROR_GENERICO } from '../reservasTextos.js';
import PortadaReservas from '../components/PortadaReservas.jsx';
import BuscarReserva from '../components/BuscarReserva.jsx';
import DetalleReserva from '../components/DetalleReserva.jsx';
import '../styles/index.css';

/** Ruta pública /mi-reserva: buscar por código+teléfono, y ver/cancelar el detalle. */
export default function MiReserva() {
  const location = useLocation();
  const navigate = useNavigate();

  // Captura síncrona en el primer render (useState perezoso, no un efecto):
  // así el formulario puede prellenarse de inmediato con el código/teléfono
  // que llegó por location.state, sin esperar a un useEffect.
  const [estadoInicial] = useState(() => location.state);
  const credencialesIniciales = estadoInicial?.codigo && estadoInicial?.telefono
    ? { codigo: estadoInicial.codigo, telefono: estadoInicial.telefono }
    : null;

  const [fase, setFase] = useState('buscar');
  const [reserva, setReserva] = useState(null);
  const [credenciales, setCredenciales] = useState(credencialesIniciales);
  const [buscando, setBuscando] = useState(Boolean(credencialesIniciales));
  const [errorInicial, setErrorInicial] = useState('');
  const [bloqueadoInicial, setBloqueadoInicial] = useState(false);

  const tituloRef = useRef(null);
  const primerMontajeRef = useRef(true);
  const yaLeyoEstadoRef = useRef(false);

  useEffect(() => {
    document.title = 'Mi reserva · GIRA';
  }, []);

  // Solo enfoca el h1 en cambios de fase reales, no en el primer montaje.
  useEffect(() => {
    if (primerMontajeRef.current) {
      primerMontajeRef.current = false;
      return;
    }
    tituloRef.current?.focus();
  }, [fase]);

  // location.state llega desde ReservaConfirmada ("Ver mi reserva"). Se limpia
  // de inmediato con replace (las credenciales no deben quedar en el historial
  // del navegador) y luego se intenta la consulta automática, con su propio
  // try/catch: si falla, la pantalla se queda en 'buscar' con el código y el
  // teléfono ya escritos y el mensaje de error visible, en vez de una promesa
  // sin manejar. yaLeyoEstadoRef evita que el doble montaje de StrictMode
  // repita la consulta.
  useEffect(() => {
    if (yaLeyoEstadoRef.current) return;
    yaLeyoEstadoRef.current = true;

    if (!credencialesIniciales) return;

    navigate(location.pathname, { replace: true, state: null });

    (async () => {
      try {
        const resultado = await reservasService.consultarPorCodigo(credencialesIniciales);
        setReserva(resultado);
        setFase('detalle');
      } catch (err) {
        if (err.response?.status === 429) {
          setBloqueadoInicial(true);
        }
        setErrorInicial(extraerMensajeError(err, MENSAJE_ERROR_GENERICO));
      } finally {
        setBuscando(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function buscar(datos) {
    setBuscando(true);
    try {
      const resultado = await reservasService.consultarPorCodigo(datos);
      setReserva(resultado);
      setCredenciales(datos);
      setFase('detalle');
    } finally {
      setBuscando(false);
    }
  }

  async function volverAConsultar() {
    const resultado = await reservasService.consultarPorCodigo(credenciales);
    setReserva(resultado);
  }

  function buscarOtra() {
    setReserva(null);
    setCredenciales(null);
    // BuscarReserva se remonta al volver a 'buscar': sin esto, seguiría
    // viendo errorInicial/bloqueadoInicial con el valor de la consulta
    // automática original y los volvería a aplicar como si fueran nuevos.
    setErrorInicial('');
    setBloqueadoInicial(false);
    setFase('buscar');
  }

  return (
    <div className="reservas-raiz">
      <div className="reservas-pagina">
        {fase === 'buscar' ? (
          <>
            <PortadaReservas
              activa="mi-reserva"
              eyebrow="MI RESERVA"
              titulo="Consulta o cancela tu reserva"
              tituloCompacto
              tituloRef={tituloRef}
            />
            <BuscarReserva
              inicial={credenciales}
              errorInicial={errorInicial}
              bloqueadoInicial={bloqueadoInicial}
              onBuscar={buscar}
              buscando={buscando}
            />
          </>
        ) : (
          <DetalleReserva
            reserva={reserva}
            credenciales={credenciales}
            onCancelada={() => setReserva((actual) => ({ ...actual, estado: 'Cancelada' }))}
            onVolverAConsultar={volverAConsultar}
            onNueva={buscarOtra}
          />
        )}
      </div>
    </div>
  );
}
