import { useEffect, useRef, useState } from 'react';
import { reservasService, codigoDeError, CODIGOS_ERROR, extraerMensajeError } from '../reservasService.js';
import { nombreZonaElegida } from '../zonasReserva.js';
import { MENSAJE_ERROR_GENERICO } from '../reservasTextos.js';
import PortadaReservas from '../components/PortadaReservas.jsx';
import PasoFecha from '../components/PasoFecha.jsx';
import PasoDatos from '../components/PasoDatos.jsx';
import ReservaConfirmada from '../components/ReservaConfirmada.jsx';
import TicketResumen from '../components/TicketResumen.jsx';
import '../styles/index.css';

function hoyIso() {
  const ahora = new Date();
  const mm = String(ahora.getMonth() + 1).padStart(2, '0');
  const dd = String(ahora.getDate()).padStart(2, '0');
  return `${ahora.getFullYear()}-${mm}-${dd}`;
}

function valorInicial() {
  return { fecha: hoyIso(), personas: 2, franja: null, zonaId: null, nombre: '', telefono: '', reserva: null };
}

/** Ruta pública /reservar: wizard de 2 pasos + confirmación, todo en una página. */
export default function Reservar() {
  const [fase, setFase] = useState('fecha');
  const [valor, setValor] = useState(valorInicial);
  // Mensaje que se muestra al volver al Paso 1 (hoy: la zona elegida se llenó antes de confirmar).
  const [avisoPaso1, setAvisoPaso1] = useState(null);
  const tituloRef = useRef(null);
  const primerMontajeRef = useRef(true);

  useEffect(() => {
    document.title = 'Reservar mesa · GIRA';
  }, []);

  // Solo enfoca el h1 en cambios de paso reales, no en el primer montaje
  // (ahí el foco ya está donde el navegador lo puso al cargar la página).
  useEffect(() => {
    if (primerMontajeRef.current) {
      primerMontajeRef.current = false;
      return;
    }
    tituloRef.current?.focus();
  }, [fase]);

  function actualizar(parcial) {
    // Cualquier cambio en la selección descarta el aviso de "zona llena": ya no aplica a lo que se eligió.
    if ('fecha' in parcial || 'personas' in parcial || 'zonaId' in parcial || (parcial.franja && parcial.franja.id !== valor.franja?.id)) {
      setAvisoPaso1(null);
    }
    setValor((actual) => ({ ...actual, ...parcial }));
  }

  // Origen nunca se manda: el backend lo ignora en el camino de invitado, y
  // aquí tampoco se arma el campo para que no quede ninguna duda.
  async function confirmar({ nombre, telefono }) {
    let reserva;
    try {
      reserva = await reservasService.crearReserva({
        nombre,
        telefono,
        fecha: valor.fecha,
        franjaHorariaId: valor.franja.id,
        personas: valor.personas,
        zonaId: valor.zonaId,
      });
    } catch (error) {
      // Se decide por el código, no por el texto: la zona elegida se llenó mientras el cliente llenaba sus
      // datos. Se vuelve al Paso 1 (que se vuelve a montar y consulta la disponibilidad fresca) con el mensaje
      // del servidor; la zona se limpia para que elija otra o "sin preferencia". Los demás errores los
      // maneja PasoDatos.
      if (codigoDeError(error) === CODIGOS_ERROR.ZONA_SIN_MESAS) {
        setAvisoPaso1(extraerMensajeError(error, MENSAJE_ERROR_GENERICO));
        setValor((actual) => ({ ...actual, nombre, telefono, zonaId: null }));
        setFase('fecha');
        return;
      }
      throw error;
    }
    setValor((actual) => ({ ...actual, nombre, telefono, reserva }));
    setFase('confirmada');
  }

  function nuevaReserva() {
    setValor(valorInicial());
    setAvisoPaso1(null);
    setFase('fecha');
  }

  if (fase === 'confirmada') {
    return (
      <div className="reservas-raiz">
        <div className="reservas-pagina">
          <ReservaConfirmada reserva={valor.reserva} telefono={valor.telefono} onNueva={nuevaReserva} />
        </div>
      </div>
    );
  }

  return (
    <div className="reservas-raiz">
      <div className="reservas-pagina">
        {fase === 'fecha' ? (
          <PortadaReservas
            activa="reservar"
            badge
            eyebrow="RESERVAS EN LÍNEA"
            titulo="Tu mesa lista,"
            acento="sin hacer una llamada"
            subtitulo="Elige el día, cuántos son y tu franja. Recibes un código al instante, sin crear cuenta."
            tituloRef={tituloRef}
          />
        ) : (
          <PortadaReservas
            activa="reservar"
            corta
            eyebrow="CASI LISTO"
            titulo="¿A nombre de quién reservamos?"
            tituloCompacto
            tituloRef={tituloRef}
          />
        )}

        <div className="reservas-tarjeta">
          <div className="reservas-tarjeta-principal">
            {fase === 'fecha' && (
              <PasoFecha
                valor={{ fecha: valor.fecha, personas: valor.personas, franja: valor.franja, zonaId: valor.zonaId }}
                aviso={avisoPaso1}
                onCambiar={actualizar}
                onContinuar={() => setFase('datos')}
              />
            )}
            {fase === 'datos' && (
              <PasoDatos
                valor={{ nombre: valor.nombre, telefono: valor.telefono }}
                onCambiar={actualizar}
                onAtras={() => setFase('fecha')}
                onConfirmar={confirmar}
              />
            )}
          </div>

          <TicketResumen
            fecha={valor.fecha}
            franja={valor.franja}
            personas={valor.personas}
            zonaNombre={nombreZonaElegida(valor.franja, valor.zonaId)}
            onCambiar={fase === 'datos' ? () => setFase('fecha') : undefined}
          />
        </div>
      </div>
    </div>
  );
}
