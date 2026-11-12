import { useState } from 'react';
import EstadoPagina from '../components/EstadoPagina.jsx';
import FormularioVisita from '../components/FormularioVisita.jsx';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import { useFranjas } from '../hooks/useFranjas.js';
import { horaActualSantoDomingo } from '../reservasPersonalFormato.js';
import { AYUDA_SESION_VISITAS, OPCION_SIN_MESA, TITULO_SESION_VISITAS, textoPersonas } from '../reservasPersonalTextos.js';

const MAXIMO_EN_SESION = 5;

/**
 * Ruta /reservas/visitas (Lote 4, GI-396): registro de visitas sin reserva. El backend no tiene
 * lectura de visitas, así que la lista «Registradas en esta sesión» vive solo en memoria de esta
 * pantalla (se pierde al recargar) y no guarda nada personal: personas, franja y mesa.
 */
export default function VisitasPersonal() {
  const { franjas, estado, error, reintentar } = useFranjas();
  const [sesion, setSesion] = useState([]);

  function alRegistrar(visita) {
    setSesion((actual) => [
      { id: visita.id, hora: horaActualSantoDomingo(), personas: visita.personas, franja: visita.franjaNombre, mesa: visita.mesaNumero },
      ...actual,
    ].slice(0, MAXIMO_EN_SESION));
  }

  return (
    <PaginaPersonal clave="visitas">
      {estado === 'cargando' && <EstadoPagina estado="cargando" />}
      {estado === 'error' && <EstadoPagina estado="error" mensaje={error} onReintentar={reintentar} />}
      {estado === 'listo' && (
        <div className="reservas-personal-tarjeta reservas-personal-tarjeta--formulario">
          <FormularioVisita franjas={franjas} enPagina onRegistrada={alRegistrar} />
        </div>
      )}

      {sesion.length > 0 && (
        <section className="reservas-personal-tarjeta" aria-labelledby="visitas-sesion">
          <h2 id="visitas-sesion" className="reservas-personal-subtitulo">{TITULO_SESION_VISITAS}</h2>
          <p className="reservas-campo-ayuda">{AYUDA_SESION_VISITAS}</p>
          <ul className="reservas-personal-sesion">
            {sesion.map((visita) => (
              <li key={visita.id}>
                <strong>{visita.hora}</strong> · {textoPersonas(visita.personas)} · {visita.franja} · {visita.mesa ? `Mesa ${visita.mesa}` : OPCION_SIN_MESA}
              </li>
            ))}
          </ul>
        </section>
      )}
    </PaginaPersonal>
  );
}
