import { formatearHora } from '../../reservasFormato.js';
import { ESTADOS_FILTRO, esFechaIso, hoyEnSantoDomingo, sumarDias } from '../reservasPersonalFormato.js';
import {
  AYUDA_BUSQUEDA,
  ESTADOS_RESERVA_PERSONAL,
  ETIQUETA_BUSQUEDA,
  TEXTO_BUSQUEDA_TODAS_LAS_FECHAS,
  TEXTO_FILTRO_TODAS,
  TEXTO_FILTRO_TODOS,
  TEXTO_FRANJAS_NO_DISPONIBLES,
  TEXTO_FRANJA_INACTIVA,
} from '../reservasPersonalTextos.js';
import Aviso from '../../components/Aviso.jsx';

function etiquetaFranja(franja) {
  const horas = `${formatearHora(franja.horaInicio)}–${formatearHora(franja.horaFin)}`;
  const base = `${franja.nombre} · ${horas}`;
  return franja.activa ? base : `${base} (${TEXTO_FRANJA_INACTIVA})`;
}

/**
 * Filtros de la agenda: fecha, estado (uno), franja y el campo único «Código o teléfono» (GI-401). Con 8 dígitos
 * busca el código y con un teléfono válido (10 dígitos) busca por teléfono, solos 300 ms después de la última
 * tecla; Enter o «Buscar» buscan de inmediato. Los demás filtros se aplican al cambiar. El campo no lleva `name` y
 * usa autoComplete="off": lo escrito vive solo en el estado de la página (ni storage, ni URL, ni consola).
 */
export default function FiltrosAgenda({ agenda }) {
  const {
    aplicados, busqueda, clasificacion, franjas, franjasFallaron,
    cambiarFecha, cambiarEstado, cambiarFranja, escribirBusqueda, buscarAhora, limpiar,
  } = agenda;
  const porTelefono = aplicados.telefono !== '';
  const invalida = clasificacion.tipo === 'invalida';
  // La pista «sigue escribiendo» es neutra; solo lo inválido es un error.
  const hayMensaje = clasificacion.mensaje !== '';

  function alEnviar(evento) {
    evento.preventDefault();
    buscarAhora();
  }

  // Solo el campo de búsqueda y «Buscar» pertenecen al <form> (por el atributo `form`): pulsar Enter en la fecha o
  // en un selector no dispara la búsqueda. El resto de filtros se aplica al cambiar.
  return (
    <div className="reservas-personal-tarjeta reservas-personal-filtros">
      <form id="agenda-form-busqueda" onSubmit={alEnviar} noValidate />
      <div className="reservas-personal-filtros-grid">
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="agenda-fecha">Fecha</label>
          <div className="reservas-personal-fecha">
            <button
              type="button"
              className="reservas-personal-icono-boton"
              aria-label="Día anterior"
              onClick={() => cambiarFecha(sumarDias(aplicados.fecha, -1))}
              disabled={porTelefono}
            >‹</button>
            <input
              id="agenda-fecha"
              type="date"
              className="reservas-input"
              value={aplicados.fecha}
              onChange={(evento) => { if (esFechaIso(evento.target.value)) cambiarFecha(evento.target.value); }}
              disabled={porTelefono}
              aria-describedby={porTelefono ? 'agenda-fecha-ayuda' : undefined}
            />
            <button
              type="button"
              className="reservas-personal-icono-boton"
              aria-label="Día siguiente"
              onClick={() => cambiarFecha(sumarDias(aplicados.fecha, 1))}
              disabled={porTelefono}
            >›</button>
            <button
              type="button"
              className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
              onClick={() => cambiarFecha(hoyEnSantoDomingo())}
              disabled={porTelefono}
            >Hoy</button>
          </div>
          {porTelefono && <span id="agenda-fecha-ayuda" className="reservas-campo-ayuda">{TEXTO_BUSQUEDA_TODAS_LAS_FECHAS}</span>}
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="agenda-estado">Estado</label>
          <select
            id="agenda-estado"
            className="reservas-input"
            value={aplicados.estado}
            onChange={(evento) => cambiarEstado(evento.target.value)}
          >
            <option value="">{TEXTO_FILTRO_TODOS}</option>
            {ESTADOS_FILTRO.map((estado) => (
              <option key={estado} value={estado}>{ESTADOS_RESERVA_PERSONAL[estado].etiqueta}</option>
            ))}
          </select>
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="agenda-franja">Franja</label>
          <select
            id="agenda-franja"
            className="reservas-input"
            value={aplicados.franjaId}
            onChange={(evento) => cambiarFranja(evento.target.value)}
            disabled={franjasFallaron}
          >
            <option value="">{TEXTO_FILTRO_TODAS}</option>
            {franjas.map((franja) => (
              <option key={franja.id} value={franja.id}>{etiquetaFranja(franja)}</option>
            ))}
          </select>
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="agenda-busqueda">{ETIQUETA_BUSQUEDA}</label>
          <input
            id="agenda-busqueda"
            form="agenda-form-busqueda"
            type="tel"
            inputMode="tel"
            autoComplete="off"
            className="reservas-input"
            placeholder="Código o 809 555 0199"
            maxLength={20}
            value={busqueda}
            onChange={(evento) => escribirBusqueda(evento.target.value)}
            aria-invalid={invalida ? 'true' : undefined}
            aria-describedby={hayMensaje ? 'agenda-busqueda-ayuda agenda-busqueda-mensaje' : 'agenda-busqueda-ayuda'}
          />
          <span id="agenda-busqueda-ayuda" className="reservas-campo-ayuda">{AYUDA_BUSQUEDA}</span>
          {hayMensaje && (
            <span
              id="agenda-busqueda-mensaje"
              className={`reservas-campo-ayuda${invalida ? ' reservas-campo-ayuda--error' : ''}`}
              role={invalida ? 'alert' : undefined}
            >
              {clasificacion.mensaje}
            </span>
          )}
        </div>
      </div>

      <div className="reservas-personal-filtros-acciones">
        <button
          type="submit"
          form="agenda-form-busqueda"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
        >
          Buscar
        </button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={limpiar}
        >Limpiar</button>
      </div>

      {franjasFallaron && <Aviso tipo="advertencia">{TEXTO_FRANJAS_NO_DISPONIBLES}</Aviso>}
    </div>
  );
}
