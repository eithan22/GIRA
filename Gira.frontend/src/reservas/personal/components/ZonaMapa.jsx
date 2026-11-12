import { accionMesa } from '../reservasPersonalFormato.js';
import { TEXTO_ZONA_INACTIVA, TEXTO_ZONA_SIN_MESAS, textoZonaResumen } from '../reservasPersonalTextos.js';
import MesaMapa from './MesaMapa.jsx';

/**
 * Una zona del mapa como sección con su título, su conteo y sus mesas. Las zonas inactivas se ven
 * atenuadas y con la etiqueta «Zona inactiva».
 */
export default function ZonaMapa({ zona, puedeVisita, onAbrirReserva, onRegistrarVisita }) {
  const idTitulo = `zona-${zona.zonaId}`;
  return (
    <section
      className={`reservas-personal-zona${zona.activa ? '' : ' reservas-personal-zona--inactiva'}`}
      aria-labelledby={idTitulo}
    >
      <header className="reservas-personal-zona-cabecera">
        <h3 id={idTitulo} className="reservas-personal-zona-titulo">
          {zona.nombre}
          {!zona.activa && <span className="reservas-personal-etiqueta-inactiva"> · {TEXTO_ZONA_INACTIVA}</span>}
        </h3>
        <span className="reservas-personal-secundario">{textoZonaResumen(zona.resumen.libres, zona.resumen.total)}</span>
      </header>

      {zona.mesas.length === 0 ? (
        <p className="reservas-personal-secundario">{TEXTO_ZONA_SIN_MESAS}</p>
      ) : (
        <ul className="reservas-personal-mesas">
          {zona.mesas.map((mesa) => (
            <MesaMapa
              key={mesa.mesaId}
              mesa={mesa}
              zonaNombre={zona.nombre}
              accion={accionMesa(mesa, { puedeVisita })}
              onAbrirReserva={(m, boton) => onAbrirReserva(zona, m, boton)}
              onRegistrarVisita={(m, boton) => onRegistrarVisita(zona, m, boton)}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
