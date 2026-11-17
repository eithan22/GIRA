import { useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearHora } from '../../reservasFormato.js';
import { useAdvertenciaMesa } from '../hooks/useAdvertenciaMesa.js';
import { useAhora } from '../hooks/useAhora.js';
import { useRestauranteAbierto } from '../hooks/useRestauranteAbierto.js';
import { useMesasLibres } from '../hooks/useMesasLibres.js';
import {
  estadoFranjasVisita,
  franjaDefectoVisita,
  franjasParaVisita,
  inicioPrimeraFranja,
  mesasParaVisita,
  motivoMesaNoDisponibleVisita,
  textoSinMesasVisita,
  textoUltimaFranja,
  validarPersonasVisita,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_MESA_VISITA_SIN_MARCA,
  AVISO_VISITA_DEFINITIVA,
  ETIQUETA_MESA_VISITA,
  ETIQUETA_PERSONAS_VISITA,
  MENSAJE_MESA_OBLIGATORIA,
  TEXTO_ACTUALIZAR_MESAS,
  TEXTO_CARGANDO_MESAS,
  TEXTO_CONFIRMAR_VISITA,
  TEXTO_CARGANDO_FRANJAS,
  TEXTO_ERROR_FRANJAS,
  TEXTO_FRANJA_EN_CURSO,
  TEXTO_RESTAURANTE_CERRADO_HOY,
  textoRestauranteCerradoAhora,
  TEXTO_REGISTRANDO_VISITA,
  TEXTO_REGISTRAR_OTRA,
  TEXTO_REGISTRAR_VISITA,
  TEXTO_REINTENTAR,
  TEXTO_SIN_FRANJAS_VISITA,
  TEXTO_VOLVER,
  TITULO_VISITA_REGISTRADA,
  textoConfirmarVisita,
  textoMesasLibres,
  textoPersonas,
  textoSinFranjaEmpezada,
} from '../reservasPersonalTextos.js';

const PERSONAS_INICIALES = '2';

/**
 * Formulario de visita sin reserva (POST /api/visitas), el mismo en el panel de la Agenda y en el del Mapa (que
 * además lo abre desde una mesa libre). No pide teléfono ni datos personales.
 * - «Hoy» y «ya empezó» salen de Santo Domingo con Intl (useAhora); el servidor decide y su 409
 *   se muestra tal cual.
 * - Antes de enviar hay una confirmación en línea («No se podrá editar ni borrar»); el doble
 *   envío está bloqueado con un ref y con el botón deshabilitado.
 * - Las mesas libres salen del mapa de ocupación de hoy en esa franja.
 * - Franjas: `franjasEstado` ('cargando' | 'listo' | 'error', el de useFranjas), `franjasError` y `onReintentarFranjas`.
 *   Mientras llegan o si fallan se dice eso (con «Reintentar»), no «No hay franjas activas»; ese aviso queda solo
 *   para cuando llegan de verdad sin ninguna activa.
 */
export default function FormularioVisita({
  franjas, franjasEstado = 'listo', franjasError = '', onReintentarFranjas,
  mesaInicialId = '', onRegistrada, onCerrar, onEnviando,
}) {
  const { hoy, hora } = useAhora();
  // Cerrado ahora (día o fecha cerrados, o fuera del horario): solo informativo; el servidor decide y su 409 se muestra
  // tal cual. Si el horario no llega, no se bloquea nada.
  const { restaurante } = useRestauranteAbierto({ hoy, hora });
  const restauranteCerrado = Boolean(restaurante) && restaurante.estado !== 'abierto';
  const avisoCerrado = restauranteCerrado && (
    <Aviso tipo="advertencia" rol="alert">
      {restaurante.estado === 'cerradoHoy'
        ? TEXTO_RESTAURANTE_CERRADO_HOY
        : textoRestauranteCerradoAhora(restaurante.apertura, restaurante.cierre)}
    </Aviso>
  );
  const franjasVisita = franjasParaVisita(franjas, hora);
  const defecto = franjaDefectoVisita(franjasVisita);

  const [personas, setPersonas] = useState(PERSONAS_INICIALES);
  const [mesaId, setMesaId] = useState(mesaInicialId);
  const [mesaPerdida, setMesaPerdida] = useState(false);
  const [motivoMesaPerdida, setMotivoMesaPerdida] = useState('');
  const [fase, setFase] = useState('editar'); // 'editar' | 'confirmar' | 'hecho'
  const [aviso, setAviso] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [registrada, setRegistrada] = useState(null);

  const envioEnCursoRef = useRef(false);
  const volverRef = useRef(null);
  const exitoRef = useRef(null);

  // Ya no hay elección del usuario: siempre la franja vigente (la actual o, entre franjas, la última que empezó).
  const franjaId = defecto?.id ?? '';

  const mesas = useMesasLibres({ fecha: hoy, franjaId });

  const personasSinMesa = validarPersonasVisita(personas);
  const personasParaFiltrar = personasSinMesa.valido ? personasSinMesa.valor : 1;
  const opciones = mesas.mapa ? mesasParaVisita(mesas.mapa, personasParaFiltrar) : [];
  const mesaElegida = opciones.find((opcion) => opcion.mesaId === mesaId) ?? null;
  const validacionPersonas = validarPersonasVisita(personas, mesaElegida?.capacidad ?? null);

  // Aviso no bloqueante: si la mesa elegida tiene una reserva vigente en una franja posterior de hoy.
  const advertenciaMesa = useAdvertenciaMesa({ mesaId: mesaElegida?.mesaId ?? '', franjaId, fecha: hoy });
  const hayAvisoMesa = advertenciaMesa.estado === 'listo' && Boolean(advertenciaMesa.advertencia?.hayAviso);

  // Si la mesa preseleccionada (desde el mapa, o porque cambiaron las personas) deja de ser elegible, se
  // quita y se avisa con el motivo específico: la mesa es obligatoria, así que nunca se registra sin ella
  // por sorpresa ni con un aviso genérico cuando se sabe la causa real.
  useEffect(() => {
    if (mesaId && mesas.estado === 'listo' && !opciones.some((opcion) => opcion.mesaId === mesaId)) {
      setMotivoMesaPerdida(motivoMesaNoDisponibleVisita(mesas.mapa, mesaId, personasParaFiltrar));
      setMesaId('');
      setMesaPerdida(true);
    }
  });

  useEffect(() => {
    if (fase === 'confirmar') volverRef.current?.focus();
    if (fase === 'hecho') exitoRef.current?.focus();
  }, [fase]);

  const esperandoMesa = Boolean(mesaId) && mesas.estado !== 'listo';
  const puedeRevisar = Boolean(franjaId) && Boolean(mesaElegida) && validacionPersonas.valido
    && !esperandoMesa && !enviando && !restauranteCerrado;

  async function registrar() {
    if (envioEnCursoRef.current) return;
    envioEnCursoRef.current = true;
    setEnviando(true);
    onEnviando?.(true);
    setAviso('');
    try {
      const visita = await reservasPersonalService.registrarVisita({
        franjaId, mesaId: mesaElegida?.mesaId ?? '', personas: validacionPersonas.valor,
      });
      setRegistrada(visita);
      setFase('hecho');
      onRegistrada?.(visita);
    } catch (fallo) {
      setAviso(mensajeDeError(fallo));
      setFase('editar');
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
      onEnviando?.(false);
    }
  }

  function registrarOtra() {
    setRegistrada(null);
    setPersonas(PERSONAS_INICIALES);
    setMesaId('');
    setMesaPerdida(false);
    setMotivoMesaPerdida('');
    setAviso('');
    setFase('editar');
  }

  const estadoFranjas = estadoFranjasVisita(franjasEstado, franjasVisita.length);

  // Cargando o con error: el mismo bloque para las dos, así la región en vivo no se desmonta al pasar de una a otra
  // (los lectores de pantalla anuncian el cambio de texto) y el foco no se mueve.
  if (estadoFranjas === 'cargando' || estadoFranjas === 'error') {
    const fallo = estadoFranjas === 'error';
    return (
      <div className="reservas-personal-bloque">
        <p
          className={`reservas-personal-estado-franjas${fallo ? ' reservas-campo-ayuda--error' : ''}`}
          role="status"
          aria-live="polite"
          aria-atomic="true"
        >
          {fallo ? (franjasError || TEXTO_ERROR_FRANJAS) : TEXTO_CARGANDO_FRANJAS}
        </p>
        {fallo && onReintentarFranjas && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={onReintentarFranjas}
          >{TEXTO_REINTENTAR}</button>
        )}
      </div>
    );
  }

  if (franjasVisita.length === 0) {
    return <Aviso tipo="neutro">{TEXTO_SIN_FRANJAS_VISITA}</Aviso>;
  }

  if (!defecto) {
    // Cerrado y sin ninguna franja empezada (p. ej. de madrugada): se dice que está cerrado, no solo que no empezó ninguna.
    return avisoCerrado || (
      <Aviso tipo="neutro">{textoSinFranjaEmpezada(formatearHora(inicioPrimeraFranja(franjas)))}</Aviso>
    );
  }

  if (fase === 'hecho' && registrada) {
    return (
      <div className="reservas-personal-creada">
        <div ref={exitoRef} tabIndex={-1} className="reservas-personal-bloque">
          {/* El contenedor recibe el foco: el Aviso va sin rol de región en vivo para no leerlo dos veces. */}
          <Aviso tipo="exito" rol="none" titulo={`${TITULO_VISITA_REGISTRADA}.`}>
            {textoPersonas(registrada.personas)} · {registrada.franjaNombre} · Mesa {registrada.mesaNumero}
          </Aviso>
        </div>
        <Aviso tipo="info">{AVISO_MESA_VISITA_SIN_MARCA}</Aviso>
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={registrarOtra}
          >{TEXTO_REGISTRAR_OTRA}</button>
          {onCerrar && (
            <button
              type="button"
              className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
              onClick={onCerrar}
            >Cerrar</button>
          )}
        </div>
      </div>
    );
  }

  if (fase === 'confirmar') {
    return (
      <section className="reservas-personal-confirmacion" role="alertdialog" aria-labelledby="visita-confirmar-texto">
        <p id="visita-confirmar-texto" className="reservas-personal-confirmacion-titulo">
          {textoConfirmarVisita(validacionPersonas.valor, mesaElegida?.numero)}
        </p>
        {hayAvisoMesa && (
          <Aviso tipo="advertencia" rol="alert">{advertenciaMesa.advertencia.mensaje}</Aviso>
        )}
        {advertenciaMesa.estado === 'error' && (
          <Aviso tipo="neutro">No pudimos comprobar si esta mesa tiene una reserva más tarde; puedes registrar la visita igual.</Aviso>
        )}
        {aviso && <Aviso tipo="error">{aviso}</Aviso>}
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={registrar}
            disabled={enviando}
          >{enviando ? TEXTO_REGISTRANDO_VISITA : (hayAvisoMesa ? 'Registrar de todos modos' : TEXTO_CONFIRMAR_VISITA)}</button>
          <button
            type="button"
            ref={volverRef}
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => setFase('editar')}
            disabled={enviando}
          >{hayAvisoMesa ? 'Cancelar' : TEXTO_VOLVER}</button>
        </div>
      </section>
    );
  }

  return (
    <form
      className="reservas-personal-formulario"
      onSubmit={(evento) => { evento.preventDefault(); if (puedeRevisar) setFase('confirmar'); }}
      noValidate
    >
      {avisoCerrado}

      <fieldset className="reservas-personal-grupo">
        <legend className="reservas-campo-etiqueta">Franja</legend>
        <div className="reservas-personal-opciones">
          {/* Solo la franja vigente, ya seleccionada: ya no hay lista de 4 ni nada que elegir aquí. readOnly
              (no disabled) para que el lector de pantalla la anuncie como un radio marcado, no como uno
              deshabilitado/no disponible; sigue teniendo el input checked para no perder el borde morado
              de :has(input:checked) del CSS actual. */}
          <label className="reservas-personal-opcion">
            <input type="radio" checked readOnly />
            <span>
              <strong>{defecto.nombre} · {formatearHora(defecto.horaInicio)}–{formatearHora(defecto.horaFin)}</strong>
              <small>
                {defecto.estado === 'enCurso' ? TEXTO_FRANJA_EN_CURSO : textoUltimaFranja(restaurante?.cierre)}
              </small>
            </span>
          </label>
        </div>
      </fieldset>

      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="visita-personas">{ETIQUETA_PERSONAS_VISITA} *</label>
        <input
          id="visita-personas"
          type="number"
          inputMode="numeric"
          min={1}
          step={1}
          autoComplete="off"
          className="reservas-input"
          value={personas}
          onChange={(evento) => setPersonas(evento.target.value)}
          aria-invalid={personas !== '' && !validacionPersonas.valido ? 'true' : undefined}
          aria-describedby={personas !== '' && !validacionPersonas.valido ? 'visita-personas-error' : undefined}
        />
        {personas !== '' && !validacionPersonas.valido && (
          <span id="visita-personas-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
            {validacionPersonas.mensaje}
          </span>
        )}
      </div>

      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="visita-mesa">{ETIQUETA_MESA_VISITA} *</label>
        <select
          id="visita-mesa"
          className="reservas-input"
          value={mesaElegida?.mesaId ?? ''}
          onChange={(evento) => { setMesaId(evento.target.value); setMesaPerdida(false); setMotivoMesaPerdida(''); }}
          disabled={mesas.estado === 'cargando' && !mesas.mapa}
          aria-invalid={!mesaElegida ? 'true' : undefined}
          aria-describedby="visita-mesa-estado"
        >
          <option value="" disabled>Elige una mesa</option>
          {opciones.map((opcion) => (
            <option key={opcion.mesaId} value={opcion.mesaId}>
              {`Mesa ${opcion.numero} · ${opcion.zonaNombre} · capacidad ${opcion.capacidad}`}
            </option>
          ))}
        </select>
        <p id="visita-mesa-estado" className="reservas-campo-ayuda" role="status" aria-live="polite">
          {mesas.estado === 'cargando' && TEXTO_CARGANDO_MESAS}
          {mesas.estado === 'listo' && (opciones.length > 0 ? textoMesasLibres(opciones.length) : textoSinMesasVisita(personasParaFiltrar))}
        </p>
        {mesas.estado === 'listo' && opciones.length > 0 && !mesaElegida && (
          <span className="reservas-campo-ayuda" role="alert">{MENSAJE_MESA_OBLIGATORIA}</span>
        )}
        {mesaPerdida && (
          <Aviso tipo="advertencia">{motivoMesaPerdida || 'La mesa que elegiste ya no está disponible para este grupo. Elige otra mesa.'}</Aviso>
        )}
        {mesas.estado === 'error' && <Aviso tipo="error">{mesas.error}</Aviso>}
        {(mesas.estado === 'listo' || mesas.estado === 'error') && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={mesas.reintentar}
          >{TEXTO_ACTUALIZAR_MESAS}</button>
        )}
      </div>

      <p className="reservas-personal-confirmacion-texto">{AVISO_VISITA_DEFINITIVA}</p>
      {aviso && <Aviso tipo="error">{aviso}</Aviso>}

      <div className="reservas-personal-confirmacion-botones">
        <button
          type="submit"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          disabled={!puedeRevisar}
        >{TEXTO_REGISTRAR_VISITA}</button>
        {onCerrar && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={onCerrar}
            disabled={enviando}
          >Cancelar</button>
        )}
      </div>
    </form>
  );
}
