import { useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { useDisponibilidadCrear } from '../hooks/useDisponibilidadCrear.js';
import {
  PERSONAS_POR_DEFECTO,
  esBorradorCrear,
  fechaEnRango,
  formatearTelefono,
  franjasConEstado,
  hoyEnSantoDomingo,
  rangoFechasReserva,
  soloDigitos,
  sumarDias,
  validarNombreCliente,
  validarPersonas,
  validarTelefono,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_NOMBRE_SE_ACTUALIZA,
  MENSAJE_FECHA_FUERA_DE_RANGO,
  MENSAJE_FRANJA_OBLIGATORIA,
  MENSAJE_ORIGEN_OBLIGATORIO,
  TEXTO_CANCELAR_FORMULARIO,
  TEXTO_CREANDO,
  TEXTO_CREAR_RESERVA,
  TEXTO_MESA_AUTOMATICA_CREAR,
  TEXTO_NUEVA_RESERVA,
  TEXTO_ORIGEN_LEYENDA,
  TEXTO_ORIGEN_PRESENCIAL,
  TEXTO_ORIGEN_TELEFONO,
  TITULO_RESERVA_CREADA,
} from '../reservasPersonalTextos.js';
import BuscadorCliente from './BuscadorCliente.jsx';
import ConfirmarDescartar from './ConfirmarDescartar.jsx';
import PanelPersonal from './PanelPersonal.jsx';
import ReservaCreada from './ReservaCreada.jsx';
import SelectorFranjas from './SelectorFranjas.jsx';

function formularioInicial(hoy, origen = '') {
  return { origen, nombre: '', telefono: '', fecha: hoy, personas: PERSONAS_POR_DEFECTO, franjaId: '' };
}

/**
 * Panel «Nueva reserva» del personal (origen Teléfono o Presencial). Todo lo escrito vive aquí, en
 * memoria: el teléfono nunca va a la URL, al storage ni a la consola, y solo sale en el cuerpo del
 * POST. La mesa no se elige (la asigna el servidor). Un solo botón envía; `envioEnCursoRef` y el
 * botón deshabilitado evitan el doble envío. Cerrar con borrador pide confirmación.
 */
export default function PanelNuevaReserva({
  franjas, franjasFallaron, origenRef, fallbackFocoRef, onCerrar, onCreada, onVerEnAgenda,
}) {
  const [hoy] = useState(() => hoyEnSantoDomingo());
  const [formulario, setFormulario] = useState(() => formularioInicial(hoy));
  const [cliente, setCliente] = useState(null);
  const [intentado, setIntentado] = useState(false);
  const [aviso, setAviso] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [creada, setCreada] = useState(null); // { reserva, origen }
  const [mesa, setMesa] = useState({ estado: 'cargando', datos: null });
  const [descartando, setDescartando] = useState(false);

  const envioEnCursoRef = useRef(false);
  const montadoRef = useRef(true);
  const origenInputRef = useRef(null);
  const nombreRef = useRef(null);
  const telefonoRef = useRef(null);
  const fechaRef = useRef(null);
  const personasRef = useRef(null);
  const franjaRef = useRef(null);

  useEffect(() => {
    montadoRef.current = true;
    return () => { montadoRef.current = false; };
  }, []);

  const disponibilidad = useDisponibilidadCrear({ fecha: formulario.fecha, personas: formulario.personas, hoy });
  const franjasEstado = franjasConEstado(franjas, disponibilidad.disponibles);
  const { min, max } = rangoFechasReserva(hoy);

  // Si la franja elegida deja de tener disponibilidad al cambiar fecha o personas, se desmarca.
  useEffect(() => {
    if (!formulario.franjaId || disponibilidad.estado !== 'listo') return;
    const elegida = franjasEstado.find((franja) => franja.id === formulario.franjaId);
    if (!elegida || elegida.disponible === false) setFormulario((actual) => ({ ...actual, franjaId: '' }));
  });

  function cambiar(campo, valor) {
    setFormulario((actual) => ({ ...actual, [campo]: valor }));
  }

  const validacionNombre = validarNombreCliente(formulario.nombre);
  const validacionTelefono = validarTelefono(formulario.telefono);
  const validacionPersonas = validarPersonas(formulario.personas);
  const fechaValida = fechaEnRango(formulario.fecha, hoy);
  const franjaValida = formulario.franjaId !== '';

  const errores = {
    origen: formulario.origen ? '' : MENSAJE_ORIGEN_OBLIGATORIO,
    nombre: validacionNombre.valido ? '' : validacionNombre.mensaje,
    telefono: validacionTelefono.valido ? '' : validacionTelefono.mensaje,
    fecha: fechaValida ? '' : MENSAJE_FECHA_FUERA_DE_RANGO,
    personas: validacionPersonas.valido ? '' : validacionPersonas.mensaje,
    franja: franjaValida ? '' : MENSAJE_FRANJA_OBLIGATORIA,
  };
  const mostrar = (campo) => (intentado ? errores[campo] : '');

  const nombreCambiado = cliente && soloDigitos(formulario.telefono) === cliente.telefono
    && formulario.nombre.trim() !== cliente.nombre;

  const borrador = esBorradorCrear(formulario, hoy, cliente);

  function usarCliente(elegido) {
    setCliente(elegido);
    setFormulario((actual) => ({ ...actual, nombre: elegido.nombre, telefono: formatearTelefono(elegido.telefono) }));
    nombreRef.current?.focus();
  }

  function enfocarPrimerInvalido() {
    const orden = [
      ['origen', origenInputRef], ['nombre', nombreRef], ['telefono', telefonoRef],
      ['fecha', fechaRef], ['personas', personasRef], ['franja', franjaRef],
    ];
    const primero = orden.find(([campo]) => errores[campo]);
    primero?.[1].current?.focus();
    return Boolean(primero);
  }

  async function cargarMesa(reserva, franjaId) {
    try {
      const pagina = await reservasPersonalService.listarReservas({
        fecha: reserva.fecha, franjaId, page: 1, pageSize: 100,
      });
      const fila = (pagina?.items ?? []).find((item) => item.codigo === reserva.codigo);
      if (montadoRef.current) setMesa(fila ? { estado: 'listo', datos: fila } : { estado: 'sin', datos: null });
    } catch {
      if (montadoRef.current) setMesa({ estado: 'sin', datos: null });
    }
  }

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setAviso('');
    if (enfocarPrimerInvalido()) return;

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const reserva = await reservasPersonalService.crearReserva({
        nombre: formulario.nombre,
        telefono: formulario.telefono,
        fecha: formulario.fecha,
        franjaHorariaId: formulario.franjaId,
        personas: formulario.personas,
        origen: formulario.origen,
      });
      setMesa({ estado: 'cargando', datos: null });
      setCreada({ reserva, origen: formulario.origen });
      onCreada();
      cargarMesa(reserva, formulario.franjaId);
    } catch (fallo) {
      setAviso(mensajeDeError(fallo));
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  function crearOtra() {
    setCreada(null);
    setCliente(null);
    setIntentado(false);
    setAviso('');
    setFormulario(formularioInicial(hoy, formulario.origen));
  }

  function intentarCerrar() {
    if (descartando) {
      setDescartando(false);
      return;
    }
    if (!creada && borrador) setDescartando(true);
    else onCerrar();
  }

  const titulo = creada ? TITULO_RESERVA_CREADA : TEXTO_NUEVA_RESERVA;

  return (
    <PanelPersonal
      titulo={titulo}
      tituloId="panel-nueva-titulo"
      eyebrow="RESERVAS"
      onCerrar={intentarCerrar}
      bloqueado={enviando}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      {descartando && <ConfirmarDescartar onSeguir={() => setDescartando(false)} onDescartar={onCerrar} />}

      {creada ? (
        <ReservaCreada
          reserva={creada.reserva}
          origen={creada.origen}
          mesa={mesa}
          onVerEnAgenda={() => onVerEnAgenda(creada.reserva.fecha, creada.reserva.codigo)}
          onCrearOtra={crearOtra}
        />
      ) : (
        <form className="reservas-personal-formulario" onSubmit={enviar} noValidate>
          <fieldset className="reservas-personal-grupo" aria-describedby={mostrar('origen') ? 'origen-error' : undefined}>
            <legend className="reservas-campo-etiqueta">{TEXTO_ORIGEN_LEYENDA} *</legend>
            <div className="reservas-personal-opciones reservas-personal-opciones--fila">
              {[['Telefono', TEXTO_ORIGEN_TELEFONO], ['Presencial', TEXTO_ORIGEN_PRESENCIAL]].map(([valor, etiqueta], indice) => (
                <label key={valor} className="reservas-personal-opcion">
                  <input
                    type="radio"
                    name="origen-nueva-reserva"
                    value={valor}
                    ref={indice === 0 ? origenInputRef : undefined}
                    checked={formulario.origen === valor}
                    onChange={() => cambiar('origen', valor)}
                  />
                  <span><strong>{etiqueta}</strong></span>
                </label>
              ))}
            </div>
            {mostrar('origen') && (
              <span id="origen-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.origen}</span>
            )}
          </fieldset>

          <BuscadorCliente onUsar={usarCliente} deshabilitado={enviando} />

          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="nueva-nombre">Nombre *</label>
            <input
              id="nueva-nombre"
              ref={nombreRef}
              type="text"
              autoComplete="off"
              className="reservas-input"
              value={formulario.nombre}
              onChange={(evento) => cambiar('nombre', evento.target.value)}
              aria-invalid={mostrar('nombre') ? 'true' : undefined}
              aria-describedby={mostrar('nombre') ? 'nueva-nombre-error' : undefined}
            />
            {mostrar('nombre') && (
              <span id="nueva-nombre-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.nombre}</span>
            )}
            {nombreCambiado && <Aviso tipo="advertencia">{AVISO_NOMBRE_SE_ACTUALIZA}</Aviso>}
          </div>

          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="nueva-telefono">Teléfono *</label>
            <input
              id="nueva-telefono"
              ref={telefonoRef}
              type="tel"
              inputMode="tel"
              autoComplete="off"
              className="reservas-input"
              placeholder="809 555 0199"
              maxLength={20}
              value={formulario.telefono}
              onChange={(evento) => cambiar('telefono', evento.target.value)}
              aria-invalid={mostrar('telefono') ? 'true' : undefined}
              aria-describedby={mostrar('telefono') ? 'nueva-telefono-error' : undefined}
            />
            {mostrar('telefono') && (
              <span id="nueva-telefono-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.telefono}</span>
            )}
          </div>

          <div className="reservas-personal-fila-campos">
            <div className="reservas-campo">
              <label className="reservas-campo-etiqueta" htmlFor="nueva-fecha">Fecha *</label>
              <div className="reservas-personal-fecha">
                <button
                  type="button"
                  className="reservas-personal-icono-boton"
                  aria-label="Día anterior"
                  onClick={() => cambiar('fecha', sumarDias(formulario.fecha, -1))}
                  disabled={formulario.fecha <= min}
                >‹</button>
                <input
                  id="nueva-fecha"
                  ref={fechaRef}
                  type="date"
                  className="reservas-input"
                  min={min}
                  max={max}
                  value={formulario.fecha}
                  onChange={(evento) => cambiar('fecha', evento.target.value)}
                  aria-invalid={mostrar('fecha') ? 'true' : undefined}
                  aria-describedby={mostrar('fecha') ? 'nueva-fecha-error' : undefined}
                />
                <button
                  type="button"
                  className="reservas-personal-icono-boton"
                  aria-label="Día siguiente"
                  onClick={() => cambiar('fecha', sumarDias(formulario.fecha, 1))}
                  disabled={formulario.fecha >= max}
                >›</button>
              </div>
              {mostrar('fecha') && (
                <span id="nueva-fecha-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.fecha}</span>
              )}
            </div>

            <div className="reservas-campo">
              <label className="reservas-campo-etiqueta" htmlFor="nueva-personas">Personas *</label>
              <input
                id="nueva-personas"
                ref={personasRef}
                type="number"
                inputMode="numeric"
                min={1}
                step={1}
                autoComplete="off"
                className="reservas-input"
                value={formulario.personas}
                onChange={(evento) => cambiar('personas', evento.target.value)}
                aria-invalid={mostrar('personas') ? 'true' : undefined}
                aria-describedby={mostrar('personas') ? 'nueva-personas-error' : undefined}
              />
              {mostrar('personas') && (
                <span id="nueva-personas-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.personas}</span>
              )}
            </div>
          </div>

          <SelectorFranjas
            franjas={franjasEstado}
            valor={formulario.franjaId}
            onCambiar={(id) => cambiar('franjaId', id)}
            estado={franjasFallaron ? 'error' : disponibilidad.estado}
            error={franjasFallaron ? 'No pudimos cargar las franjas.' : disponibilidad.error}
            onReintentar={disponibilidad.reintentar}
            mensajeCampo={mostrar('franja')}
            legendRef={franjaRef}
          />

          <p className="reservas-personal-confirmacion-texto">{TEXTO_MESA_AUTOMATICA_CREAR}</p>

          {aviso && <Aviso tipo="error">{aviso}</Aviso>}

          <div className="reservas-personal-confirmacion-botones">
            <button
              type="submit"
              className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
              disabled={enviando}
            >{enviando ? TEXTO_CREANDO : TEXTO_CREAR_RESERVA}</button>
            <button
              type="button"
              className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
              onClick={intentarCerrar}
              disabled={enviando}
            >{TEXTO_CANCELAR_FORMULARIO}</button>
          </div>
        </form>
      )}
    </PanelPersonal>
  );
}
