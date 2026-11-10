import { useEffect, useRef, useState } from 'react';
import { reservasService } from '../reservasService.js';
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
  return { fecha: hoyIso(), personas: 2, franja: null, nombre: '', telefono: '', reserva: null };
}

/** Ruta pública /reservar: wizard de 2 pasos + confirmación, todo en una página. */
export default function Reservar() {
  const [fase, setFase] = useState('fecha');
  const [valor, setValor] = useState(valorInicial);
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
    setValor((actual) => ({ ...actual, ...parcial }));
  }

  // Origen nunca se manda: el backend lo ignora en el camino de invitado, y
  // aquí tampoco se arma el campo para que no quede ninguna duda.
  async function confirmar({ nombre, telefono }) {
    const reserva = await reservasService.crearReserva({
      nombre,
      telefono,
      fecha: valor.fecha,
      franjaHorariaId: valor.franja.id,
      personas: valor.personas,
    });
    setValor((actual) => ({ ...actual, nombre, telefono, reserva }));
    setFase('confirmada');
  }

  function nuevaReserva() {
    setValor(valorInicial());
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
                valor={{ fecha: valor.fecha, personas: valor.personas, franja: valor.franja }}
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
            onCambiar={fase === 'datos' ? () => setFase('fecha') : undefined}
          />
        </div>
      </div>
    </div>
  );
}
