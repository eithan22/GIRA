const ROL_POR_TIPO = {
  error: 'alert',
  exito: 'status',
  advertencia: 'note',
  info: 'status',
  neutro: 'status',
};

// error/advertencia: "!"; éxito: check; info/neutro: "i" (nunca un check en
// neutro — ese círculo es solo informativo, no confirma nada).
function IconoAviso({ tipo }) {
  if (tipo === 'error' || tipo === 'advertencia') {
    return (
      <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 8v5M12 16.5v.01" />
      </svg>
    );
  }

  if (tipo === 'exito') {
    return (
      <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
        <path d="M5 12.5l4.5 4.5L19 7.5" />
      </svg>
    );
  }

  return (
    <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8v.01" />
    </svg>
  );
}

/**
 * Caja de aviso reutilizable para los 5 estados del CSS (07-avisos.css).
 * Por defecto: error -> role="alert"; éxito/info/neutro -> role="status";
 * advertencia -> role="note". `rol` sobreescribe eso — se usa con rol="alert"
 * en los avisos de 429, que siguen viéndose ámbar (tipo="advertencia") pero
 * deben anunciarse como una alerta.
 */
export default function Aviso({ tipo, titulo, children, rol }) {
  return (
    <div className={`reservas-aviso reservas-aviso--${tipo}`} role={rol ?? ROL_POR_TIPO[tipo]}>
      <span className="reservas-aviso-icono"><IconoAviso tipo={tipo} /></span>
      <span>
        {titulo && <strong>{titulo} </strong>}
        {children}
      </span>
    </div>
  );
}
