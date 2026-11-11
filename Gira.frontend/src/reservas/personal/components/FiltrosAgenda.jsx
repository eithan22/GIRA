import { formatearHora } from '../../reservasFormato.js';
import { ESTADOS_FILTRO, esFechaIso, hoyEnSantoDomingo, sumarDias } from '../reservasPersonalFormato.js';
import {
  ESTADOS_RESERVA_PERSONAL,
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
 * Filtros de la agenda: fecha, estado (uno), franja y teléfono. El teléfono se envía con el botón
 * «Buscar» o con Enter; los demás filtros se aplican al cambiar. El campo no lleva `name` y usa
 * autoComplete="off": el valor vive solo en el estado de la página.
 */
export default function FiltrosAgenda({ agenda }) {
  const {
    aplicados, telefonoEscrito, errorTelefono, franjas, franjasFallaron,
    cambiarFecha, cambiarEstado, cambiarFranja, escribirTelefono, buscarPorTelefono, limpiar,
  } = agenda;
  const porTelefono = aplicados.telefono !== '';

  function alEnviar(evento) {
    evento.preventDefault();
    buscarPorTelefono();
  }

  return (
    <form className="reservas-personal-tarjeta reservas-personal-filtros" onSubmit={alEnviar} noValidate>
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
          <label className="reservas-campo-etiqueta" htmlFor="agenda-telefono">Teléfono del cliente</label>
          <input
            id="agenda-telefono"
            type="tel"
            inputMode="tel"
            autoComplete="off"
            className="reservas-input"
            placeholder="809 555 0199"
            maxLength={20}
            value={telefonoEscrito}
            onChange={(evento) => escribirTelefono(evento.target.value)}
            aria-invalid={errorTelefono ? 'true' : undefined}
            aria-describedby={errorTelefono ? 'agenda-telefono-error' : undefined}
          />
          {errorTelefono && (
            <span id="agenda-telefono-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
              {errorTelefono}
            </span>
          )}
        </div>
      </div>

      <div className="reservas-personal-filtros-acciones">
        <button type="submit" className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto">
          Buscar
        </button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={limpiar}
        >Limpiar</button>
      </div>

      {franjasFallaron && <Aviso tipo="advertencia">{TEXTO_FRANJAS_NO_DISPONIBLES}</Aviso>}
    </form>
  );
}
