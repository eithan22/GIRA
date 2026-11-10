import { Link } from 'react-router-dom';

/**
 * Portada oscura compartida por /reservar y /mi-reserva: logo, menú Reservar/Mi
 * reserva y el bloque de texto de cada paso. `badge` dibuja el eyebrow como
 * píldora con punto (Main/MovilReservar); sin `badge` se dibuja como texto
 * plano (Paso2/Consulta/Detalle). `children` deja que la página meta contenido
 * propio (por ejemplo, el enlace "Buscar otra reserva" + h1 de Detalle).
 */
export default function PortadaReservas({
  activa, eyebrow, badge, titulo, tituloRef, tituloCompacto, acento, subtitulo, corta, children,
}) {
  return (
    <div className={`reservas-portada${corta ? ' reservas-portada--corta' : ''}`}>
      <div className="reservas-portada-decoracion reservas-portada-decoracion--a" aria-hidden="true" />
      <div className="reservas-portada-decoracion reservas-portada-decoracion--b" aria-hidden="true" />

      <header className="reservas-header">
        <div className="reservas-marca">
          <span className="reservas-marca-icono">G</span>
          <span className="reservas-marca-nombre">
            GIRA<span className="reservas-marca-punto">.</span>
          </span>
        </div>
        <nav className="reservas-nav" aria-label="Reservas">
          <Link
            to="/reservar"
            className={`reservas-nav-link${activa === 'reservar' ? ' reservas-nav-link--activo' : ''}`}
            aria-current={activa === 'reservar' ? 'page' : undefined}
          >
            Reservar
          </Link>
          <Link
            to="/mi-reserva"
            className={`reservas-nav-link${activa === 'mi-reserva' ? ' reservas-nav-link--activo' : ''}`}
            aria-current={activa === 'mi-reserva' ? 'page' : undefined}
          >
            Mi reserva
          </Link>
        </nav>
      </header>

      {(eyebrow || titulo || subtitulo || children) && (
        <div className={`reservas-hero${corta ? ' reservas-hero--corta' : ''}`}>
          {eyebrow && badge && (
            <span className="reservas-hero-badge">
              <span className="reservas-hero-punto" aria-hidden="true" />
              {eyebrow}
            </span>
          )}
          {eyebrow && !badge && <span className="reservas-hero-eyebrow">{eyebrow}</span>}

          {titulo && (
            <h1
              ref={tituloRef}
              tabIndex={-1}
              className={`reservas-hero-titulo${tituloCompacto ? ' reservas-hero-titulo--compacta' : ''}`}
            >
              {titulo}
              {acento && <span className="reservas-hero-titulo--acento"> {acento}</span>}
            </h1>
          )}

          {subtitulo && <p className="reservas-hero-subtitulo">{subtitulo}</p>}

          {children}
        </div>
      )}
    </div>
  );
}
