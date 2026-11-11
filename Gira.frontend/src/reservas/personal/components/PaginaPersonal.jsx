import { useEffect, useRef } from 'react';
import { PANTALLAS, TEXTO_PROXIMAMENTE } from '../reservasPersonalTextos.js';
import EstadoPagina from './EstadoPagina.jsx';
import NavegacionReservas from './NavegacionReservas.jsx';
import '../styles/index.css';

/**
 * Esqueleto común de las pantallas del personal: raíz con las variables de
 * Eithan, document.title, h1 enfocable (foco al cambiar `fase`, no en el primer
 * montaje) y el estado «Próximamente» mientras no haya contenido.
 * `clave` es una de las claves de PANTALLAS; `fase` cambia cuando la pantalla
 * cambia de paso; `children` reemplaza el «Próximamente».
 */
export default function PaginaPersonal({ clave, fase, tituloRef: tituloExterno, children }) {
  const pantalla = PANTALLAS[clave];
  const tituloInterno = useRef(null);
  // La página puede pasar su propio ref (por ejemplo, para devolver el foco al h1 al cerrar un panel).
  const tituloRef = tituloExterno ?? tituloInterno;
  const primerMontajeRef = useRef(true);

  useEffect(() => {
    document.title = `${pantalla.titulo} · GIRA`;
  }, [pantalla.titulo]);

  useEffect(() => {
    if (primerMontajeRef.current) {
      primerMontajeRef.current = false;
      return;
    }
    tituloRef.current?.focus();
  }, [fase]);

  return (
    <div className="reservas-raiz reservas-personal">
      <section className="reservas-personal-pagina">
        <NavegacionReservas />
        <header className="reservas-personal-encabezado">
          <span className="reservas-personal-eyebrow">{pantalla.eyebrow}</span>
          <h1 ref={tituloRef} tabIndex={-1} className="reservas-personal-titulo">{pantalla.titulo}</h1>
          <p className="reservas-personal-descripcion">{pantalla.descripcion}</p>
        </header>
        {children ?? <EstadoPagina estado="vacio" mensaje={TEXTO_PROXIMAMENTE} />}
      </section>
    </div>
  );
}
