import { useRef, useState } from 'react';
import { normalizarHora, validarHorarioFranja, validarNombreFranja } from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import { ETIQUETA_HORA_FIN, ETIQUETA_HORA_INICIO, ETIQUETA_NOMBRE } from '../reservasPersonalTextos.js';
import PanelFormularioSalon from './PanelFormularioSalon.jsx';

// El <input type="time"> entrega y espera "HH:mm"; la API habla en "HH:mm:ss".
const aHoraInput = (hora) => (hora ? hora.slice(0, 5) : '');

/**
 * Crear o editar una franja (POST /api/franjas, PUT /api/franjas/{id}). Valida en el cliente solo lo
 * que el backend: nombre obligatorio y de hasta 50 caracteres, y fin posterior al inicio. Las horas se
 * normalizan a "HH:mm:ss". Nombre repetido, solapamiento y cambiar el horario con reservas futuras:
 * 409 del servidor, mostrado tal cual.
 */
export default function PanelFranja({ item, origenRef, fallbackFocoRef, onCerrar, onGuardada }) {
  const inicial = { nombre: item?.nombre ?? '', inicio: aHoraInput(item?.horaInicio), fin: aHoraInput(item?.horaFin) };
  const [nombre, setNombre] = useState(inicial.nombre);
  const [inicio, setInicio] = useState(inicial.inicio);
  const [fin, setFin] = useState(inicial.fin);
  const [intentado, setIntentado] = useState(false);
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const nombreRef = useRef(null);
  const inicioRef = useRef(null);
  const finRef = useRef(null);

  const vNombre = validarNombreFranja(nombre);
  const vHorario = validarHorarioFranja(inicio, fin);
  const borrador = nombre.trim() !== inicial.nombre
    || normalizarHora(inicio) !== normalizarHora(inicial.inicio)
    || normalizarHora(fin) !== normalizarHora(inicial.fin);

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setError('');
    const invalido = [[!vNombre.valido, nombreRef], [Boolean(vHorario.errores.inicio), inicioRef], [Boolean(vHorario.errores.fin), finRef]]
      .find(([malo]) => malo);
    if (invalido) {
      invalido[1].current?.focus();
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const franja = await reservasPersonalService.guardarFranja(
        { nombre, horaInicio: vHorario.inicio, horaFin: vHorario.fin }, item?.id,
      );
      onGuardada(franja, !item);
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  return (
    <PanelFormularioSalon
      titulo={item ? `Editar franja ${item.nombre}` : 'Nueva franja'}
      eyebrow="FRANJAS"
      borrador={borrador}
      enviando={enviando}
      error={error}
      onSubmit={enviar}
      onCerrar={onCerrar}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="franja-nombre">{ETIQUETA_NOMBRE} *</label>
        <input
          id="franja-nombre"
          ref={nombreRef}
          type="text"
          autoComplete="off"
          className="reservas-input"
          value={nombre}
          onChange={(evento) => setNombre(evento.target.value)}
          aria-invalid={intentado && !vNombre.valido ? 'true' : undefined}
          aria-describedby={intentado && !vNombre.valido ? 'franja-nombre-error' : undefined}
        />
        {intentado && !vNombre.valido && (
          <span id="franja-nombre-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vNombre.mensaje}</span>
        )}
      </div>

      <div className="reservas-personal-fila-campos">
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="franja-inicio">{ETIQUETA_HORA_INICIO} *</label>
          <input
            id="franja-inicio"
            ref={inicioRef}
            type="time"
            step={60}
            className="reservas-input"
            value={inicio}
            onChange={(evento) => setInicio(evento.target.value)}
            aria-invalid={intentado && vHorario.errores.inicio ? 'true' : undefined}
            aria-describedby={intentado && vHorario.errores.inicio ? 'franja-inicio-error' : undefined}
          />
          {intentado && vHorario.errores.inicio && (
            <span id="franja-inicio-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vHorario.errores.inicio}</span>
          )}
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="franja-fin">{ETIQUETA_HORA_FIN} *</label>
          <input
            id="franja-fin"
            ref={finRef}
            type="time"
            step={60}
            className="reservas-input"
            value={fin}
            onChange={(evento) => setFin(evento.target.value)}
            aria-invalid={intentado && vHorario.errores.fin ? 'true' : undefined}
            aria-describedby={intentado && vHorario.errores.fin ? 'franja-fin-error' : undefined}
          />
          {intentado && vHorario.errores.fin && (
            <span id="franja-fin-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vHorario.errores.fin}</span>
          )}
        </div>
      </div>
    </PanelFormularioSalon>
  );
}
