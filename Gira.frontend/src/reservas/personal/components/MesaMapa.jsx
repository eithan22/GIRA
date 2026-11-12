import { esConflictoMesaInactiva } from '../reservasPersonalFormato.js';
import {
  ESTADOS_MESA,
  TEXTO_ACCION_ABRIR_RESERVA,
  TEXTO_ACCION_REGISTRAR_VISITA,
  textoConflictoMesaInactiva,
} from '../reservasPersonalTextos.js';

function textoPersonas(mesa) {
  if (mesa.personas == null) return '';
  return mesa.personasReales != null ? `${mesa.personas} → ${mesa.personasReales} personas` : `${mesa.personas} personas`;
}

/**
 * Una mesa del mapa. Si tiene acción es un <button> real (nunca un div clicable); si no, es una
 * tarjeta informativa. El estado lleva símbolo y texto además de color. Una mesa inactiva con
 * reserva vigente muestra el conflicto y abre su reserva.
 */
export default function MesaMapa({ mesa, zonaNombre, accion, onAbrirReserva, onRegistrarVisita }) {
  const info = ESTADOS_MESA[mesa.estado] ?? ESTADOS_MESA.Inactiva;
  const conflicto = esConflictoMesaInactiva(mesa);
  const personas = textoPersonas(mesa);

  const contenido = (
    <>
      <span className="reservas-personal-mesa-numero">Mesa {mesa.numero}</span>
      <span className="reservas-personal-secundario">Capacidad {mesa.capacidad}</span>
      <span className="reservas-personal-mesa-estado">
        <span aria-hidden="true">{info.simbolo}</span> {info.etiqueta}
      </span>
      {conflicto && <span className="reservas-personal-mesa-conflicto">{textoConflictoMesaInactiva(mesa.clienteNombre)}</span>}
      {!conflicto && mesa.clienteNombre && <span className="reservas-personal-ajustable"><strong>{mesa.clienteNombre}</strong></span>}
      {personas && <span className="reservas-personal-secundario">{personas}</span>}
      {accion === 'registrarVisita' && <span className="reservas-personal-mesa-accion">{TEXTO_ACCION_REGISTRAR_VISITA}</span>}
      {accion === 'abrirReserva' && <span className="reservas-personal-mesa-accion">{TEXTO_ACCION_ABRIR_RESERVA}</span>}
    </>
  );

  const clases = `reservas-personal-mesa reservas-personal-mesa--${info.clase}${conflicto ? ' reservas-personal-mesa--conflicto' : ''}`;

  if (accion === 'abrirReserva') {
    return (
      <li>
        <button
          type="button"
          className={clases}
          onClick={(evento) => onAbrirReserva(mesa, evento.currentTarget)}
          aria-label={`Mesa ${mesa.numero}, ${zonaNombre}, capacidad ${mesa.capacidad}, ${info.etiqueta}${conflicto ? `, ${textoConflictoMesaInactiva(mesa.clienteNombre)}` : mesa.clienteNombre ? `, ${mesa.clienteNombre}` : ''}${personas ? `, ${personas}` : ''}. ${TEXTO_ACCION_ABRIR_RESERVA}`}
        >
          {contenido}
        </button>
      </li>
    );
  }

  if (accion === 'registrarVisita') {
    return (
      <li>
        <button
          type="button"
          className={clases}
          onClick={(evento) => onRegistrarVisita(mesa, evento.currentTarget)}
          aria-label={`Mesa ${mesa.numero}, ${zonaNombre}, capacidad ${mesa.capacidad}, ${info.etiqueta}. ${TEXTO_ACCION_REGISTRAR_VISITA}`}
        >
          {contenido}
        </button>
      </li>
    );
  }

  return (
    <li>
      <div className={clases}>{contenido}</div>
    </li>
  );
}
