import { formatearFechaLarga, formatearHora } from '../../reservasFormato.js';
import { ESTADOS_MESA, textoResumenMapa } from '../reservasPersonalTextos.js';

/**
 * Contadores del mapa (libres, reservadas, ocupadas, inactivas) con texto, símbolo y color, y una
 * frase en aria-live="polite" que anuncia el resumen al cargar. La frase no lleva la hora de
 * actualización, así que un refresco sin cambios no se vuelve a anunciar.
 */
export default function ResumenMapa({ mapa }) {
  const { resumen } = mapa;
  const frase = textoResumenMapa({
    franja: mapa.franjaNombre,
    horas: `${formatearHora(mapa.horaInicio)}–${formatearHora(mapa.horaFin)}`,
    fechaLarga: formatearFechaLarga(mapa.fecha),
    resumen,
  });
  const contadores = [
    ['Libre', resumen.libres, resumen.libres === 1 ? 'Libre' : 'Libres'],
    ['Reservada', resumen.reservadas, resumen.reservadas === 1 ? 'Reservada' : 'Reservadas'],
    ['Ocupada', resumen.ocupadas, resumen.ocupadas === 1 ? 'Ocupada' : 'Ocupadas'],
    ['Inactiva', resumen.inactivas, resumen.inactivas === 1 ? 'Inactiva' : 'Inactivas'],
  ];

  return (
    <div className="reservas-personal-tarjeta reservas-personal-resumen-mapa">
      <p className="reservas-solo-lectores" role="status" aria-live="polite">{frase}</p>
      <ul className="reservas-personal-contadores" aria-hidden="true">
        {contadores.map(([estado, cantidad, etiqueta]) => (
          <li key={estado} className={`reservas-personal-contador reservas-personal-mesa--${ESTADOS_MESA[estado].clase}`}>
            <span className="reservas-personal-contador-simbolo">{ESTADOS_MESA[estado].simbolo}</span>
            <strong>{cantidad}</strong> {etiqueta}
          </li>
        ))}
      </ul>
    </div>
  );
}
