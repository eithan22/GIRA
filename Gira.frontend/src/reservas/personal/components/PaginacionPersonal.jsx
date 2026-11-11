import { paginasTotales } from '../reservasPersonalFormato.js';

/** Paginación Anterior / Siguiente con «Página x de y». No se dibuja si hay una sola página. */
export default function PaginacionPersonal({ page, total, pageSize, deshabilitada, onIr }) {
  const paginas = paginasTotales(total, pageSize);
  if (paginas <= 1) return null;

  return (
    <nav className="reservas-personal-paginacion" aria-label="Paginación de reservas">
      <button
        type="button"
        className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
        onClick={() => onIr(page - 1)}
        disabled={deshabilitada || page <= 1}
      >Anterior</button>
      <span className="reservas-personal-paginacion-texto">Página {page} de {paginas}</span>
      <button
        type="button"
        className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
        onClick={() => onIr(page + 1)}
        disabled={deshabilitada || page >= paginas}
      >Siguiente</button>
    </nav>
  );
}
