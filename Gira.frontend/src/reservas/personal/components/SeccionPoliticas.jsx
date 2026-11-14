import { useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { errorPorCampo } from '../../reservasErrores.js';
import { useCargaConfiguracion } from '../hooks/useCargaConfiguracion.js';
import { invalidarParametrosOperativos } from '../hooks/useParametrosOperativos.js';
import {
  CAMPOS_POLITICAS,
  formatearFechaHoraSantoDomingo,
  formularioAPoliticas,
  politicasAFormulario,
  politicasIguales,
  textoLegiblePolitica,
  validarPoliticas,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_RECOMENDADOS_CARGADOS,
  ERROR_CORREGIR_CAMPOS,
  EXITO_POLITICAS,
  INTRO_POLITICAS,
  TEXTOS_CAMPOS_POLITICAS,
  TEXTO_CARGANDO_CONFIGURACION,
  TEXTO_CONFIGURACION_NO_CARGO,
  TEXTO_DESCARTAR_CAMBIOS,
  TEXTO_GUARDANDO,
  TEXTO_GUARDAR_POLITICAS,
  TEXTO_RESTABLECER,
  textoPorDefecto,
  textoUltimaActualizacion,
} from '../reservasPersonalTextos.js';
import CampoNumericoConfiguracion from './CampoNumericoConfiguracion.jsx';
import EstadoPagina from './EstadoPagina.jsx';

/**
 * Pestaña «Políticas» (GI-226, RF-CFG-01): las seis políticas de reserva con su unidad y el texto legible al lado,
 * ayuda breve por campo, «Restablecer valores recomendados» (carga los valores por defecto SIN guardar hasta que se
 * pulse Guardar; «Descartar cambios» vuelve a lo guardado) y los errores del servidor junto a cada campo.
 */
export default function SeccionPoliticas({ onSucio }) {
  const carga = useCargaConfiguracion((opciones) => reservasPersonalService.obtenerParametrosReserva(opciones));

  if (carga.estado === 'cargando') return <EstadoPagina estado="cargando" mensaje={TEXTO_CARGANDO_CONFIGURACION} />;
  if (carga.estado === 'error') {
    return <EstadoPagina estado="error" mensaje={carga.error || TEXTO_CONFIGURACION_NO_CARGO} onReintentar={carga.recargar} />;
  }
  return <FormularioPoliticas dto={carga.datos} onSucio={onSucio} />;
}

function FormularioPoliticas({ dto, onSucio }) {
  const [base, setBase] = useState(() => politicasAFormulario(dto.parametros));
  const [formulario, setFormulario] = useState(base);
  const [actualizadoEn, setActualizadoEn] = useState(dto.actualizadoEn);
  const [restablecido, setRestablecido] = useState(false);
  const [intentado, setIntentado] = useState(false);
  const [erroresServidor, setErroresServidor] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [exito, setExito] = useState(null); // { advertencias }
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const campoRefs = useRef({});
  const avisoRef = useRef(null);

  const defectos = politicasAFormulario(dto.valoresPorDefecto);
  const hayCambios = !politicasIguales(formulario, base);
  const erroresCliente = intentado ? validarPoliticas(formulario) : {};
  const errores = { ...erroresCliente, ...erroresServidor };

  useEffect(() => {
    onSucio(hayCambios);
  }, [hayCambios, onSucio]);

  // El aviso de éxito o de «recomendados cargados» recibe el foco: es lo que cambió.
  useEffect(() => {
    if (exito || restablecido) avisoRef.current?.focus();
  }, [exito, restablecido]);

  function cambiar(clave, valor) {
    setFormulario((actual) => ({ ...actual, [clave]: valor }));
    setErroresServidor((actuales) => {
      if (!actuales[clave]) return actuales;
      const { [clave]: _quitado, ...resto } = actuales;
      return resto;
    });
    setExito(null);
  }

  function restablecer() {
    setFormulario(defectos);
    setRestablecido(true);
    setExito(null);
    setErroresServidor({});
    setErrorGeneral('');
    setIntentado(false);
  }

  function descartar() {
    setFormulario(base);
    setRestablecido(false);
    setExito(null);
    setErroresServidor({});
    setErrorGeneral('');
    setIntentado(false);
  }

  function enfocarPrimerError(campos) {
    const primero = CAMPOS_POLITICAS.find(({ clave }) => campos[clave]);
    if (primero) campoRefs.current[primero.clave]?.focus();
  }

  async function guardar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setErrorGeneral('');
    setExito(null);
    const invalidos = validarPoliticas(formulario);
    if (Object.keys(invalidos).length > 0) {
      setErroresServidor({});
      enfocarPrimerError(invalidos);
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const resultado = await reservasPersonalService.guardarParametrosReserva(formularioAPoliticas(formulario));
      const guardado = politicasAFormulario(resultado.configuracion.parametros);
      setBase(guardado);
      setFormulario(guardado);
      setActualizadoEn(resultado.configuracion.actualizadoEn);
      setRestablecido(false);
      setErroresServidor({});
      setIntentado(false);
      setExito({ advertencias: resultado.advertencias ?? [] });
      invalidarParametrosOperativos();
    } catch (fallo) {
      const campos = errorPorCampo(fallo);
      if (Object.keys(campos).length > 0) {
        setErroresServidor(campos);
        enfocarPrimerError(campos);
      } else {
        setErrorGeneral(mensajeDeError(fallo));
      }
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  const hayErrores = Object.keys(errores).length > 0;

  return (
    <form className="reservas-personal-seccion reservas-personal-config" onSubmit={guardar} noValidate>
      <p className="reservas-personal-intro">{INTRO_POLITICAS}</p>
      {actualizadoEn && (
        <p className="reservas-campo-ayuda">{textoUltimaActualizacion(formatearFechaHoraSantoDomingo(actualizadoEn))}</p>
      )}

      {restablecido && (
        <div ref={avisoRef} tabIndex={-1}>
          <Aviso tipo="info">{AVISO_RECOMENDADOS_CARGADOS}</Aviso>
        </div>
      )}
      {exito && (
        <div ref={avisoRef} tabIndex={-1}>
          <Aviso tipo="exito" rol="none">{EXITO_POLITICAS}</Aviso>
          {exito.advertencias.map((texto) => <Aviso key={texto} tipo="advertencia">{texto}</Aviso>)}
        </div>
      )}
      {errorGeneral && <Aviso tipo="error">{errorGeneral}</Aviso>}
      {hayErrores && !errorGeneral && intentado && <Aviso tipo="error">{ERROR_CORREGIR_CAMPOS}</Aviso>}

      <div className="reservas-personal-campos-config">
        {CAMPOS_POLITICAS.map((campo) => {
          const textos = TEXTOS_CAMPOS_POLITICAS[campo.clave];
          return (
            <CampoNumericoConfiguracion
              key={campo.clave}
              id={`politica-${campo.clave}`}
              etiqueta={textos.etiqueta}
              unidad={campo.unidad}
              legible={textoLegiblePolitica(campo, formulario[campo.clave])}
              ayuda={`${textos.ayuda} ${textoPorDefecto(defectos[campo.clave], campo.unidad)}`}
              valor={formulario[campo.clave]}
              minimo={campo.minimo}
              maximo={campo.maximo}
              error={errores[campo.clave]}
              onCambiar={(valor) => cambiar(campo.clave, valor)}
              inputRef={(elemento) => { campoRefs.current[campo.clave] = elemento; }}
            />
          );
        })}
      </div>

      <div className="reservas-personal-confirmacion-botones">
        <button
          type="submit"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          disabled={enviando || !hayCambios}
        >{enviando ? TEXTO_GUARDANDO : TEXTO_GUARDAR_POLITICAS}</button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={restablecer}
          disabled={enviando}
        >{TEXTO_RESTABLECER}</button>
        {hayCambios && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--fantasma-error reservas-personal-boton-auto"
            onClick={descartar}
            disabled={enviando}
          >{TEXTO_DESCARTAR_CAMBIOS}</button>
        )}
      </div>
    </form>
  );
}
