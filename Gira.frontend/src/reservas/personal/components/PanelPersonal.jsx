import { useEffect, useRef } from 'react';

const ENFOCABLES = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Panel lateral modal del personal (en móvil ocupa toda la pantalla). No es el Modal de Seguridad:
 * - mueve el foco al título al abrirse y lo atrapa dentro del panel (Tab / Shift+Tab);
 * - Escape lo cierra, salvo mientras `bloqueado` (hay una petición en curso);
 * - NO se cierra al pulsar el fondo, para no perder lo que el usuario escribió;
 * - al cerrarse devuelve el foco al elemento de `origenRef` (el botón que lo abrió) o, si ya no
 *   existe, a `fallbackFocoRef`. Se pasa el elemento a propósito: Safari no enfoca los botones al pulsarlos.
 * Se renderiza dentro del árbol de la página (sin portal) para heredar las variables --reservas-*.
 */
export default function PanelPersonal({
  titulo, tituloId, eyebrow, onCerrar, bloqueado = false, origenRef, fallbackFocoRef, children,
}) {
  const panelRef = useRef(null);
  const tituloRef = useRef(null);

  useEffect(() => {
    tituloRef.current?.focus();
    const overflowPrevio = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = overflowPrevio; };
  }, []);

  useEffect(() => () => {
    const origen = origenRef?.current;
    if (origen && origen.isConnected) origen.focus();
    else fallbackFocoRef?.current?.focus();
  }, [origenRef, fallbackFocoRef]);

  function alTeclear(evento) {
    if (evento.key === 'Escape') {
      if (!bloqueado) {
        evento.stopPropagation();
        onCerrar();
      }
      return;
    }
    if (evento.key !== 'Tab') return;

    const enfocables = [...panelRef.current.querySelectorAll(ENFOCABLES)]
      .filter((elemento) => elemento.offsetParent !== null);
    if (enfocables.length === 0) {
      evento.preventDefault();
      tituloRef.current?.focus();
      return;
    }
    const primero = enfocables[0];
    const ultimo = enfocables[enfocables.length - 1];
    const activo = document.activeElement;
    if (evento.shiftKey && (activo === primero || activo === tituloRef.current)) {
      evento.preventDefault();
      ultimo.focus();
    } else if (!evento.shiftKey && activo === ultimo) {
      evento.preventDefault();
      primero.focus();
    }
  }

  return (
    <div className="reservas-personal-panel-fondo">
      <aside
        ref={panelRef}
        className="reservas-personal-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby={tituloId}
        onKeyDown={alTeclear}
      >
        <header className="reservas-personal-panel-cabecera">
          <div>
            {eyebrow && <span className="reservas-personal-eyebrow">{eyebrow}</span>}
            <h2 id={tituloId} ref={tituloRef} tabIndex={-1} className="reservas-personal-panel-titulo">{titulo}</h2>
          </div>
          <button
            type="button"
            className="reservas-personal-panel-cerrar"
            onClick={onCerrar}
            disabled={bloqueado}
            aria-label="Cerrar panel"
          >
            <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
              <path d="m18 6-12 12M6 6l12 12" />
            </svg>
          </button>
        </header>
        <div className="reservas-personal-panel-cuerpo">{children}</div>
      </aside>
    </div>
  );
}
