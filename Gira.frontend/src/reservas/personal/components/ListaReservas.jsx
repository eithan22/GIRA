import { formatearCodigoVisual, formatearFechaCorta, formatearHora } from '../../reservasFormato.js';
import { etiquetaOrigen, formatearTelefono } from '../reservasPersonalFormato.js';
import { ETIQUETA_NUEVA, TEXTO_RESERVA_RECIEN_CREADA } from '../reservasPersonalTextos.js';
import EstadoReservaPill from './EstadoReservaPill.jsx';

function horas(fila) {
  return `${formatearHora(fila.franjaHoraInicio)}–${formatearHora(fila.franjaHoraFin)}`;
}

function textoPersonas(fila) {
  return fila.personasReales != null ? `${fila.personas} → ${fila.personasReales}` : String(fila.personas);
}

function descripcionPersonas(fila) {
  return fila.personasReales != null
    ? `${fila.personas} reservadas, ${fila.personasReales} llegaron`
    : `${fila.personas} personas`;
}

// Marca de la reserva recién creada: texto visible y para lectores, no solo color de fondo.
function Insignia() {
  return (
    <span className="reservas-personal-insignia">
      {ETIQUETA_NUEVA}
      <span className="reservas-solo-lectores"> — {TEXTO_RESERVA_RECIEN_CREADA}</span>
    </span>
  );
}

function BotonVer({ fila, onVer }) {
  return (
    <button
      type="button"
      className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
      onClick={(evento) => onVer(fila, evento.currentTarget)}
      aria-label={`Ver reserva ${formatearCodigoVisual(fila.codigo)} de ${fila.clienteNombre}`}
    >
      Ver
    </button>
  );
}

/**
 * Lista de reservas: tabla en escritorio y tablet, tarjetas en móvil (≤720 px), sin scroll
 * horizontal. En tablet las columnas Origen y Código pasan a una segunda línea de la primera
 * celda. `mostrarFecha` se activa en la búsqueda por teléfono, que mezcla varios días.
 */
export default function ListaReservas({ items, mostrarFecha, ocupada, resaltarCodigo, onVer }) {
  return (
    <div aria-busy={ocupada ? 'true' : 'false'} className={`reservas-personal-lista${ocupada ? ' reservas-personal-lista--ocupada' : ''}`}>
      <div className="reservas-personal-tabla-envoltura">
        <table className="reservas-personal-tabla">
          <caption className="reservas-solo-lectores">Reservas</caption>
          <thead>
            <tr>
              <th scope="col">Hora</th>
              <th scope="col">Mesa</th>
              <th scope="col">Cliente</th>
              <th scope="col">Personas</th>
              <th scope="col">Estado</th>
              <th scope="col" className="reservas-personal-col-extra">Origen</th>
              <th scope="col" className="reservas-personal-col-extra">Código</th>
              <th scope="col"><span className="reservas-solo-lectores">Acciones</span></th>
            </tr>
          </thead>
          <tbody>
            {items.map((fila) => (
              <tr
                key={fila.id}
                data-codigo={fila.codigo}
                className={fila.codigo === resaltarCodigo ? 'reservas-personal-resaltada' : undefined}
              >
                <td>
                  {fila.codigo === resaltarCodigo && <Insignia />}
                  {mostrarFecha && <span className="reservas-personal-secundario">{formatearFechaCorta(fila.fecha)}</span>}
                  <strong>{horas(fila)}</strong>
                  <span className="reservas-personal-secundario">{fila.franjaNombre}</span>
                  <span className="reservas-personal-linea-extra">
                    {formatearCodigoVisual(fila.codigo)} · {etiquetaOrigen(fila.origen)}
                  </span>
                </td>
                <td>
                  <strong>Mesa {fila.mesaNumero}</strong>
                  <span className="reservas-personal-secundario">{fila.zonaNombre}</span>
                </td>
                <td>
                  <strong className="reservas-personal-ajustable">{fila.clienteNombre}</strong>
                  <span className="reservas-personal-secundario">{formatearTelefono(fila.clienteTelefono)}</span>
                </td>
                <td><span aria-label={descripcionPersonas(fila)}>{textoPersonas(fila)}</span></td>
                <td><EstadoReservaPill estado={fila.estado} /></td>
                <td className="reservas-personal-col-extra">{etiquetaOrigen(fila.origen)}</td>
                <td className="reservas-personal-col-extra">{formatearCodigoVisual(fila.codigo)}</td>
                <td className="reservas-personal-celda-accion"><BotonVer fila={fila} onVer={onVer} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="reservas-personal-tarjetas">
        {items.map((fila) => (
          <li
            key={fila.id}
            data-codigo={fila.codigo}
            className={`reservas-personal-tarjeta-reserva${fila.codigo === resaltarCodigo ? ' reservas-personal-resaltada' : ''}`}
          >
            <div className="reservas-personal-tarjeta-fila">
              <div>
                {fila.codigo === resaltarCodigo && <Insignia />}
                {mostrarFecha && <span className="reservas-personal-secundario">{formatearFechaCorta(fila.fecha)}</span>}
                <strong>{horas(fila)}</strong>
              </div>
              <EstadoReservaPill estado={fila.estado} />
            </div>
            <p className="reservas-personal-ajustable">
              <strong>{fila.clienteNombre}</strong>
              <span className="reservas-personal-secundario">{formatearTelefono(fila.clienteTelefono)}</span>
            </p>
            <p className="reservas-personal-secundario">
              Mesa {fila.mesaNumero} · {fila.zonaNombre} · <span aria-label={descripcionPersonas(fila)}>{textoPersonas(fila)} personas</span>
            </p>
            <div className="reservas-personal-tarjeta-fila">
              <span className="reservas-personal-secundario">
                {etiquetaOrigen(fila.origen)} · {formatearCodigoVisual(fila.codigo)}
              </span>
              <BotonVer fila={fila} onVer={onVer} />
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
