import { useEffect, useRef, useState } from 'react';
import Icon from '../security/components/Icon.jsx';
import { agregarNotificacion, fechaCorta, marcarLeidaLocal } from './bandejaNotificaciones.js';
import { notificacionesService } from './notificacionesService.js';

const VACIA = { notificaciones: [], noLeidas: 0 };

/** Campana de la barra superior: contador de no leídas y bandeja (RF-CFG-05); los avisos llegan sin recargar (RF-CFG-06). */
export default function CampanaNotificaciones() {
  const [bandeja, setBandeja] = useState(VACIA);
  const [abierta, setAbierta] = useState(false);
  const [error, setError] = useState(false);
  const contenedor = useRef(null);

  useEffect(() => {
    let activa = true;
    const cargar = () => notificacionesService.bandeja()
      .then((datos) => { if (activa) { setBandeja(datos); setError(false); } })
      .catch(() => { if (activa) setError(true); });
    cargar();
    const conexion = notificacionesService.conectar((nueva) => setBandeja((actual) => agregarNotificacion(actual, nueva)));
    window.addEventListener('focus', cargar);
    return () => {
      activa = false;
      window.removeEventListener('focus', cargar);
      conexion.stop();
    };
  }, []);

  useEffect(() => {
    if (!abierta) return undefined;
    const cerrarFuera = (e) => { if (!contenedor.current?.contains(e.target)) setAbierta(false); };
    const cerrarEsc = (e) => { if (e.key === 'Escape') setAbierta(false); };
    document.addEventListener('mousedown', cerrarFuera);
    document.addEventListener('keydown', cerrarEsc);
    return () => {
      document.removeEventListener('mousedown', cerrarFuera);
      document.removeEventListener('keydown', cerrarEsc);
    };
  }, [abierta]);

  function marcar(notificacion) {
    if (notificacion.leida) return;
    setBandeja((actual) => marcarLeidaLocal(actual, notificacion.id));
    notificacionesService.marcarLeida(notificacion.id).catch(() => notificacionesService.bandeja().then(setBandeja).catch(() => {}));
  }

  const { noLeidas, notificaciones } = bandeja;
  const etiqueta = noLeidas === 0 ? 'Notificaciones, ninguna sin leer' : `Notificaciones, ${noLeidas} sin leer`;

  return (
    <div className="campana" ref={contenedor}>
      <button type="button" className="campana-boton" aria-label={etiqueta} aria-expanded={abierta} aria-controls="bandeja-notificaciones" onClick={() => setAbierta((a) => !a)}>
        <Icon name="bell" size={18} />
        {noLeidas > 0 && <span className="campana-contador" aria-hidden="true">{noLeidas > 9 ? '9+' : noLeidas}</span>}
      </button>
      {abierta && (
        <section className="campana-bandeja" id="bandeja-notificaciones" aria-label="Notificaciones">
          <header className="campana-titulo"><strong>Notificaciones</strong><span>{noLeidas === 1 ? '1 sin leer' : `${noLeidas} sin leer`}</span></header>
          {error && <p className="campana-vacia">No se pudieron cargar las notificaciones.</p>}
          {!error && notificaciones.length === 0 && <p className="campana-vacia">No tienes notificaciones.</p>}
          {notificaciones.length > 0 && (
            <ul className="campana-lista">
              {notificaciones.map((n) => (
                <li key={n.id}>
                  <button type="button" className={`campana-item${n.leida ? '' : ' campana-item-nueva'}`} onClick={() => marcar(n)}>
                    <span className="campana-item-titulo">{n.titulo}{!n.leida && <span className="sr-only"> (sin leer)</span>}</span>
                    <span className="campana-item-mensaje">{n.mensaje}</span>
                    <time className="campana-item-fecha" dateTime={n.fechaCreacion}>{fechaCorta(n.fechaCreacion)}</time>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}
