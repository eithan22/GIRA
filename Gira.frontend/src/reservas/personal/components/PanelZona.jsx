import { useRef, useState } from 'react';
import { validarNombreZona } from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import { ETIQUETA_NOMBRE } from '../reservasPersonalTextos.js';
import PanelFormularioSalon from './PanelFormularioSalon.jsx';

/**
 * Crear o editar una zona (POST /api/zonas, PUT /api/zonas/{id}). Valida en el cliente solo lo que el
 * backend: nombre obligatorio y de hasta 100 caracteres. Nombre repetido y demás reglas: 409 del servidor.
 */
export default function PanelZona({ item, origenRef, fallbackFocoRef, onCerrar, onGuardada }) {
  const [nombre, setNombre] = useState(item?.nombre ?? '');
  const [intentado, setIntentado] = useState(false);
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const nombreRef = useRef(null);

  const validacion = validarNombreZona(nombre);
  const borrador = nombre.trim() !== (item?.nombre ?? '');

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setError('');
    if (!validacion.valido) {
      nombreRef.current?.focus();
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const zona = await reservasPersonalService.guardarZona({ nombre }, item?.id);
      onGuardada(zona, !item);
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  return (
    <PanelFormularioSalon
      titulo={item ? `Editar zona ${item.nombre}` : 'Nueva zona'}
      eyebrow="ZONAS"
      borrador={borrador}
      enviando={enviando}
      error={error}
      onSubmit={enviar}
      onCerrar={onCerrar}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="zona-nombre">{ETIQUETA_NOMBRE} *</label>
        <input
          id="zona-nombre"
          ref={nombreRef}
          type="text"
          autoComplete="off"
          className="reservas-input"
          value={nombre}
          onChange={(evento) => setNombre(evento.target.value)}
          aria-invalid={intentado && !validacion.valido ? 'true' : undefined}
          aria-describedby={intentado && !validacion.valido ? 'zona-nombre-error' : undefined}
        />
        {intentado && !validacion.valido && (
          <span id="zona-nombre-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
            {validacion.mensaje}
          </span>
        )}
      </div>
    </PanelFormularioSalon>
  );
}
