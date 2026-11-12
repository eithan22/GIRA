import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Aviso from '../../components/Aviso.jsx';
import { formatearHora } from '../../reservasFormato.js';
import { useAhora } from '../hooks/useAhora.js';
import { useMesasLibres } from '../hooks/useMesasLibres.js';
import {
  franjaDefectoVisita,
  franjasParaVisita,
  inicioPrimeraFranja,
  mesasSeleccionables,
  validarPersonasVisita,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_MESA_VISITA_SIN_MARCA,
  AVISO_VISITA_DEFINITIVA,
  AYUDA_MESA_VISITA,
  ETIQUETA_MESA_VISITA,
  ETIQUETA_PERSONAS_VISITA,
  OPCION_SIN_MESA,
  TEXTO_ACTUALIZAR_MESAS,
  TEXTO_CARGANDO_MESAS,
  TEXTO_CONFIRMAR_VISITA,
  TEXTO_FRANJA_EN_CURSO,
  TEXTO_FRANJA_TERMINADA,
  TEXTO_REGISTRANDO_VISITA,
  TEXTO_REGISTRAR_OTRA,
  TEXTO_REGISTRAR_VISITA,
  TEXTO_SIN_FRANJAS_VISITA,
  TEXTO_VER_MAPA,
  TEXTO_VOLVER,
  TITULO_VISITA_REGISTRADA,
  textoConfirmarVisita,
  textoMesasLibres,
  textoPersonas,
  textoSinFranjaEmpezada,
} from '../reservasPersonalTextos.js';

const PERSONAS_INICIALES = '2';

/**
 * Formulario de visita sin reserva (POST /api/visitas), el mismo en la página /reservas/visitas y en
 * el panel que abre el mapa desde una mesa libre. No pide teléfono ni datos personales.
 * - «Hoy» y «ya empezó» salen de Santo Domingo con Intl (useAhora); el servidor decide y su 409
 *   se muestra tal cual.
 * - Antes de enviar hay una confirmación en línea («No se podrá editar ni borrar»); el doble
 *   envío está bloqueado con un ref y con el botón deshabilitado.
 * - Las mesas libres salen del mapa de ocupación de hoy en esa franja.
 */
export default function FormularioVisita({
  franjas, mesaInicialId = '', enPagina = false, onRegistrada, onCerrar, onEnviando,
}) {
  const { hoy, hora } = useAhora();
  const franjasVisita = franjasParaVisita(franjas, hora);
  const defecto = franjaDefectoVisita(franjasVisita);

  const [franjaElegida, setFranjaElegida] = useState('');
  const [personas, setPersonas] = useState(PERSONAS_INICIALES);
  const [mesaId, setMesaId] = useState(mesaInicialId);
  const [mesaPerdida, setMesaPerdida] = useState(false);
  const [fase, setFase] = useState('editar'); // 'editar' | 'confirmar' | 'hecho'
  const [aviso, setAviso] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [registrada, setRegistrada] = useState(null);

  const envioEnCursoRef = useRef(false);
  const volverRef = useRef(null);
  const exitoRef = useRef(null);

  // La franja elegida por el usuario manda mientras siga siendo elegible; si no, la actual o la última empezada.
  const franjaId = franjasVisita.some((franja) => franja.id === franjaElegida && franja.elegible)
    ? franjaElegida
    : (defecto?.id ?? '');

  const mesas = useMesasLibres({ fecha: hoy, franjaId });

  const personasSinMesa = validarPersonasVisita(personas);
  const opciones = mesas.mapa
    ? mesasSeleccionables(mesas.mapa, personasSinMesa.valido ? personasSinMesa.valor : 1, null)
    : [];
  const mesaElegida = opciones.find((opcion) => opcion.mesaId === mesaId) ?? null;
  const validacionPersonas = validarPersonasVisita(personas, mesaElegida?.capacidad ?? null);

  // Si la mesa preseleccionada deja de estar libre, se quita y se avisa: nunca se registra «sin mesa» por sorpresa.
  useEffect(() => {
    if (mesaId && mesas.estado === 'listo' && !opciones.some((opcion) => opcion.mesaId === mesaId)) {
      setMesaId('');
      setMesaPerdida(true);
    }
  });

  useEffect(() => {
    if (fase === 'confirmar') volverRef.current?.focus();
    if (fase === 'hecho') exitoRef.current?.focus();
  }, [fase]);

  const esperandoMesa = Boolean(mesaId) && mesas.estado !== 'listo';
  const puedeRevisar = Boolean(franjaId) && validacionPersonas.valido && !esperandoMesa && !enviando;

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
    setAviso('');
    setFase('editar');
  }

  if (franjasVisita.length === 0) {
    return <Aviso tipo="neutro">{TEXTO_SIN_FRANJAS_VISITA}</Aviso>;
  }

  if (!defecto) {
    return (
      <Aviso tipo="neutro">{textoSinFranjaEmpezada(formatearHora(inicioPrimeraFranja(franjas)))}</Aviso>
    );
  }

  if (fase === 'hecho' && registrada) {
    return (
      <div className="reservas-personal-creada">
        <div ref={exitoRef} tabIndex={-1} className="reservas-personal-bloque">
          {/* El contenedor recibe el foco: el Aviso va sin rol de región en vivo para no leerlo dos veces. */}
          <Aviso tipo="exito" rol="none" titulo={`${TITULO_VISITA_REGISTRADA}.`}>
            {textoPersonas(registrada.personas)} · {registrada.franjaNombre} · {registrada.mesaNumero ? `Mesa ${registrada.mesaNumero}` : OPCION_SIN_MESA.toLowerCase()}
          </Aviso>
        </div>
        {registrada.mesaNumero && <Aviso tipo="info">{AVISO_MESA_VISITA_SIN_MARCA}</Aviso>}
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={registrarOtra}
          >{TEXTO_REGISTRAR_OTRA}</button>
          {enPagina && (
            <Link className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto" to="/reservas/mapa">
              {TEXTO_VER_MAPA}
            </Link>
          )}
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
        {aviso && <Aviso tipo="error">{aviso}</Aviso>}
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={registrar}
            disabled={enviando}
          >{enviando ? TEXTO_REGISTRANDO_VISITA : TEXTO_CONFIRMAR_VISITA}</button>
          <button
            type="button"
            ref={volverRef}
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => setFase('editar')}
            disabled={enviando}
          >{TEXTO_VOLVER}</button>
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
      <fieldset className="reservas-personal-grupo">
        <legend className="reservas-campo-etiqueta">Franja *</legend>
        <div className="reservas-personal-opciones">
          {franjasVisita.map((franja) => (
            <label
              key={franja.id}
              className={`reservas-personal-opcion${franja.elegible ? '' : ' reservas-personal-opcion--deshabilitada'}`}
            >
              <input
                type="radio"
                name="franja-visita"
                value={franja.id}
                checked={franjaId === franja.id}
                disabled={!franja.elegible}
                onChange={() => { setFranjaElegida(franja.id); setMesaId(''); setMesaPerdida(false); }}
              />
              <span>
                <strong>{franja.nombre} · {formatearHora(franja.horaInicio)}–{formatearHora(franja.horaFin)}</strong>
                <small>
                  {franja.estado === 'enCurso' && TEXTO_FRANJA_EN_CURSO}
                  {franja.estado === 'terminada' && TEXTO_FRANJA_TERMINADA}
                  {franja.estado === 'noEmpezo' && `Empieza a las ${formatearHora(franja.horaInicio)}`}
                </small>
              </span>
            </label>
          ))}
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
        <label className="reservas-campo-etiqueta" htmlFor="visita-mesa">{ETIQUETA_MESA_VISITA}</label>
        <select
          id="visita-mesa"
          className="reservas-input"
          value={mesaElegida?.mesaId ?? ''}
          onChange={(evento) => { setMesaId(evento.target.value); setMesaPerdida(false); }}
          disabled={mesas.estado === 'cargando' && !mesas.mapa}
          aria-describedby="visita-mesa-estado"
        >
          <option value="">{OPCION_SIN_MESA}</option>
          {opciones.map((opcion) => (
            <option key={opcion.mesaId} value={opcion.mesaId}>
              {`Mesa ${opcion.numero} · ${opcion.zonaNombre} · capacidad ${opcion.capacidad}`}
            </option>
          ))}
        </select>
        <p id="visita-mesa-estado" className="reservas-campo-ayuda" role="status" aria-live="polite">
          {mesas.estado === 'cargando' && TEXTO_CARGANDO_MESAS}
          {mesas.estado === 'listo' && textoMesasLibres(opciones.length)}
        </p>
        <span className="reservas-campo-ayuda">{AYUDA_MESA_VISITA}</span>
        {mesaPerdida && (
          <Aviso tipo="advertencia">La mesa que elegiste ya no está disponible para este grupo. Elige otra o registra la visita sin mesa.</Aviso>
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
