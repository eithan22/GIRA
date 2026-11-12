import {
  TEXTO_BUSCANDO_CODIGO,
  TEXTO_BUSQUEDA_LIMITE,
  TEXTO_CODIGO_NO_ENCONTRADO,
  TEXTO_ERROR_CODIGO,
  TEXTO_REINTENTAR,
  TEXTO_RESERVA_ENCONTRADA_ANUNCIO,
  TITULO_RESERVA_ENCONTRADA,
} from '../reservasPersonalTextos.js';
import ListaReservas from './ListaReservas.jsx';

// Texto de la región en vivo según el estado de la búsqueda por código.
function textoDeEstado(resultado) {
  switch (resultado.estado) {
    case 'cargando': return TEXTO_BUSCANDO_CODIGO;
    case 'encontrada': return TEXTO_RESERVA_ENCONTRADA_ANUNCIO;
    case 'noEncontrada': return TEXTO_CODIGO_NO_ENCONTRADO;
    case 'limite': return TEXTO_BUSQUEDA_LIMITE;
    case 'error': return resultado.mensaje || TEXTO_ERROR_CODIGO;
    default: return '';
  }
}

/**
 * Resultado de la búsqueda por código (GI-401), encima de la lista del día. La región `aria-live` está SIEMPRE en
 * el DOM (vacía cuando no hay búsqueda) para que los lectores de pantalla anuncien los cambios de estado — cargando,
 * encontrada, no encontrada, límite de búsquedas y error — sin mover el foco ni abrir nada. Los errores y el límite
 * (429) no bloquean el campo: el texto es propio y se puede reintentar. La reserva encontrada se muestra con la
 * misma fila de la agenda, así que «Ver» abre el mismo panel con sus acciones.
 */
export default function ResultadoBusquedaCodigo({ resultado, onVer, onReintentar }) {
  const texto = textoDeEstado(resultado);
  const esFallo = resultado.estado === 'error' || resultado.estado === 'limite';

  return (
    <div className="reservas-personal-resultado-codigo">
      <p
        className={`reservas-personal-estado-franjas${esFallo ? ' reservas-campo-ayuda--error' : ''}`}
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >
        {texto}
      </p>

      {esFallo && (
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onReintentar}
        >{TEXTO_REINTENTAR}</button>
      )}

      {resultado.estado === 'encontrada' && resultado.fila && (
        <section aria-labelledby="agenda-resultado-codigo">
          <h2 id="agenda-resultado-codigo" className="reservas-personal-subtitulo">{TITULO_RESERVA_ENCONTRADA}</h2>
          <ListaReservas items={[resultado.fila]} mostrarFecha ocupada={false} resaltarCodigo="" onVer={onVer} />
        </section>
      )}
    </div>
  );
}
