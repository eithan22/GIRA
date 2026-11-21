import { useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import {
  validarCapacidadMesa,
  validarNumeroMesa,
  validarZonaMesa,
  zonasParaMesa,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_ZONAS_TRUNCADAS,
  AYUDA_ZONA_MESA,
  ETIQUETA_CAPACIDAD,
  ETIQUETA_NUMERO_MESA,
  ETIQUETA_ZONA,
  TEXTO_ZONAS_NO_CARGARON,
  TEXTO_ZONA_ACTUAL,
} from '../reservasPersonalTextos.js';
import PanelFormularioSalon from './PanelFormularioSalon.jsx';

/**
 * Crear o editar una mesa (POST /api/mesas, PUT /api/mesas/{id}). Valida en el cliente solo lo que el
 * backend: número entero ≥ 1, capacidad de 1 a 50 y zona obligatoria. Número repetido (único global),
 * zona inactiva y reducir la capacidad con reservas futuras: 409 del servidor, mostrado tal cual.
 * La zona se elige entre las activas y, al editar, la actual aunque esté inactiva.
 */
export default function PanelMesa({ item, zonas, origenRef, fallbackFocoRef, onCerrar, onGuardada }) {
  const inicial = {
    numero: item ? String(item.numero) : '',
    capacidad: item ? String(item.capacidad) : '',
    zonaId: item?.zonaId ?? '',
  };
  const [numero, setNumero] = useState(inicial.numero);
  const [capacidad, setCapacidad] = useState(inicial.capacidad);
  const [zonaId, setZonaId] = useState(inicial.zonaId);
  const [intentado, setIntentado] = useState(false);
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const numeroRef = useRef(null);
  const capacidadRef = useRef(null);
  const zonaRef = useRef(null);

  const vNumero = validarNumeroMesa(numero);
  const vCapacidad = validarCapacidadMesa(capacidad);
  const vZona = validarZonaMesa(zonaId);
  const opcionesZona = zonasParaMesa(zonas.zonas, item?.zonaId ?? '');
  const borrador = numero.trim() !== inicial.numero || capacidad.trim() !== inicial.capacidad || zonaId !== inicial.zonaId;

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setError('');
    const primerInvalido = [[vNumero, numeroRef], [vCapacidad, capacidadRef], [vZona, zonaRef]].find(([v]) => !v.valido);
    if (primerInvalido) {
      primerInvalido[1].current?.focus();
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const mesa = await reservasPersonalService.guardarMesa({ numero: vNumero.valor, capacidad: vCapacidad.valor, zonaId }, item?.id);
      onGuardada(mesa, !item);
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  const mostrar = (valido) => intentado && !valido.valido;

  return (
    <PanelFormularioSalon
      titulo={item ? `Editar mesa ${item.numero}` : 'Nueva mesa'}
      eyebrow="MESAS"
      borrador={borrador}
      enviando={enviando}
      error={error}
      onSubmit={enviar}
      onCerrar={onCerrar}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      <div className="reservas-personal-fila-campos">
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="mesa-numero">{ETIQUETA_NUMERO_MESA} *</label>
          <input
            id="mesa-numero"
            ref={numeroRef}
            type="number"
            inputMode="numeric"
            min={1}
            step={1}
            autoComplete="off"
            className="reservas-input"
            value={numero}
            onChange={(evento) => setNumero(evento.target.value)}
            aria-invalid={mostrar(vNumero) ? 'true' : undefined}
            aria-describedby={mostrar(vNumero) ? 'mesa-numero-error' : undefined}
          />
          {mostrar(vNumero) && (
            <span id="mesa-numero-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vNumero.mensaje}</span>
          )}
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="mesa-capacidad">{ETIQUETA_CAPACIDAD} *</label>
          <input
            id="mesa-capacidad"
            ref={capacidadRef}
            type="number"
            inputMode="numeric"
            min={1}
            max={50}
            step={1}
            autoComplete="off"
            className="reservas-input"
            value={capacidad}
            onChange={(evento) => setCapacidad(evento.target.value)}
            aria-invalid={mostrar(vCapacidad) ? 'true' : undefined}
            aria-describedby={mostrar(vCapacidad) ? 'mesa-capacidad-error' : undefined}
          />
          {mostrar(vCapacidad) && (
            <span id="mesa-capacidad-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vCapacidad.mensaje}</span>
          )}
        </div>
      </div>

      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="mesa-zona">{ETIQUETA_ZONA} *</label>
        <select
          id="mesa-zona"
          ref={zonaRef}
          className="reservas-input"
          value={zonaId}
          onChange={(evento) => setZonaId(evento.target.value)}
          disabled={zonas.estado !== 'listo'}
          aria-invalid={mostrar(vZona) ? 'true' : undefined}
          aria-describedby="mesa-zona-ayuda"
        >
          <option value="">Elige una zona</option>
          {opcionesZona.map((zona) => (
            <option key={zona.id} value={zona.id}>
              {zona.actual && !zona.activa ? `${zona.nombre} (inactiva, ${TEXTO_ZONA_ACTUAL})` : zona.nombre}
            </option>
          ))}
        </select>
        <span id="mesa-zona-ayuda" className="reservas-campo-ayuda">{AYUDA_ZONA_MESA}</span>
        {mostrar(vZona) && <span className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{vZona.mensaje}</span>}
        {zonas.estado === 'error' && <Aviso tipo="error">{TEXTO_ZONAS_NO_CARGARON}</Aviso>}
        {zonas.truncado && <Aviso tipo="advertencia">{AVISO_ZONAS_TRUNCADAS}</Aviso>}
      </div>
    </PanelFormularioSalon>
  );
}
