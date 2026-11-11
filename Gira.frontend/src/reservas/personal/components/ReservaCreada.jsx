import { useEffect, useRef } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearCodigoVisual, formatearFechaLarga, formatearHora } from '../../reservasFormato.js';
import { etiquetaOrigen } from '../reservasPersonalFormato.js';
import {
  TEXTO_BUSCANDO_MESA,
  TEXTO_CREAR_OTRA,
  TEXTO_MESA_ASIGNADA_CONSULTA,
  TEXTO_VER_EN_AGENDA,
  TITULO_RESERVA_CREADA,
} from '../reservasPersonalTextos.js';

/**
 * Confirmación de una reserva recién creada: código, cliente, fecha, franja, personas, origen y
 * mesa. La mesa no viene en la respuesta del POST, así que `mesa` llega aparte ({estado, datos}).
 */
export default function ReservaCreada({ reserva, origen, mesa, onVerEnAgenda, onCrearOtra }) {
  const exitoRef = useRef(null);

  useEffect(() => {
    exitoRef.current?.focus();
  }, []);

  let textoMesa = TEXTO_MESA_ASIGNADA_CONSULTA;
  if (mesa.estado === 'cargando') textoMesa = TEXTO_BUSCANDO_MESA;
  else if (mesa.estado === 'listo') textoMesa = `Mesa ${mesa.datos.mesaNumero} · ${mesa.datos.zonaNombre}`;

  return (
    <div className="reservas-personal-creada">
      <div ref={exitoRef} tabIndex={-1} className="reservas-personal-bloque">
        <Aviso tipo="exito" titulo={`${TITULO_RESERVA_CREADA}.`}>
          Código <strong>{formatearCodigoVisual(reserva.codigo)}</strong>
        </Aviso>
      </div>

      <dl className="reservas-personal-datos">
        <div className="reservas-personal-dato">
          <dt>Cliente</dt>
          <dd>{reserva.nombreCliente}</dd>
        </div>
        <div className="reservas-personal-dato">
          <dt>Origen</dt>
          <dd>{etiquetaOrigen(origen)}</dd>
        </div>
        <div className="reservas-personal-dato">
          <dt>Fecha</dt>
          <dd>{formatearFechaLarga(reserva.fecha)}</dd>
        </div>
        <div className="reservas-personal-dato">
          <dt>Franja</dt>
          <dd>{reserva.franjaNombre} · {formatearHora(reserva.franjaHoraInicio)}–{formatearHora(reserva.franjaHoraFin)}</dd>
        </div>
        <div className="reservas-personal-dato">
          <dt>Personas</dt>
          <dd>{reserva.personas}</dd>
        </div>
        <div className="reservas-personal-dato">
          <dt>Mesa</dt>
          <dd role="status" aria-live="polite">{textoMesa}</dd>
        </div>
      </dl>

      <div className="reservas-personal-confirmacion-botones">
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={onVerEnAgenda}
        >{TEXTO_VER_EN_AGENDA}</button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onCrearOtra}
        >{TEXTO_CREAR_OTRA}</button>
      </div>
    </div>
  );
}
