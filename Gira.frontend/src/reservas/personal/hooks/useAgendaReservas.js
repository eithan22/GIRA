import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  armarFiltrosAgenda,
  clasificarBusqueda,
  detalleAFila,
  horaActualSantoDomingo,
  hoyEnSantoDomingo,
} from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import { useFranjas } from './useFranjas.js';

// Estado de la agenda. Lo escrito en el campo «Código o teléfono», el código y el teléfono viven SOLO aquí (estado
// de React): no se escriben en la URL, en storage ni en la consola, y los errores se muestran sin volcar la petición.

// Espera tras la última tecla antes de buscar solos (código de 8 dígitos o teléfono válido). Enter y «Buscar»
// buscan de inmediato.
const DEMORA_BUSQUEDA_MS = 300;

const RESULTADO_CODIGO_INICIAL = { estado: 'idle', fila: null };

// Estado de la búsqueda por código a partir del error de la petición. El 429 tiene texto propio en la pantalla
// (nunca el mensaje neutro del servidor) y no bloquea nada.
function resultadoDeError(error) {
  const status = error?.response?.status;
  if (status === 404) return { estado: 'noEncontrada', fila: null };
  if (status === 429) return { estado: 'limite', fila: null };
  return { estado: 'error', fila: null, mensaje: mensajeDeError(error) };
}

function filtrosIniciales() {
  return { fecha: hoyEnSantoDomingo(), estado: '', franjaId: '', telefono: '', page: 1 };
}

export function useAgendaReservas() {
  // `aplicados` son los filtros con los que se pidió la lista; `busqueda` es lo que hay en el campo único.
  const [aplicados, setAplicados] = useState(filtrosIniciales);
  const [busqueda, setBusqueda] = useState('');
  const clasificacion = useMemo(() => clasificarBusqueda(busqueda), [busqueda]);

  // Búsqueda por código: `codigoAplicado` es el código con el que se pidió; `resultadoCodigo` es su resultado:
  // { estado: 'idle' | 'cargando' | 'encontrada' | 'noEncontrada' | 'limite' | 'error', fila, mensaje? }.
  const [codigoAplicado, setCodigoAplicado] = useState('');
  const [resultadoCodigo, setResultadoCodigo] = useState(RESULTADO_CODIGO_INICIAL);
  const [versionCodigo, setVersionCodigo] = useState(0);

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [fase, setFase] = useState('cargando'); // 'cargando' | 'listo' | 'error'
  const [recargando, setRecargando] = useState(false);
  const [errorLista, setErrorLista] = useState('');
  const [actualizadoA, setActualizadoA] = useState('');
  const [version, setVersion] = useState(0);
  // Cuenta las cargas completadas (no las canceladas): la página lo usa para enfocar una fila tras cargar.
  const [cargas, setCargas] = useState(0);

  // Franjas operativas (las mismas que usa el Mapa): con su estado de carga, el texto del error y «reintentar», que
  // el panel de «Visita sin reserva» muestra en vez de decir "no hay franjas" mientras llegan o si fallan.
  const {
    franjas, estado: franjasEstado, error: franjasError, reintentar: reintentarFranjas,
  } = useFranjas();
  const franjasFallaron = franjasEstado === 'error';

  // Código de la reserva recién creada que la agenda resalta (se quita al cambiar de filtro o página).
  const [resaltado, setResaltado] = useState('');

  // Lista: se pide al cambiar los filtros o al pulsar «Actualizar»; cambiar de filtro cancela la anterior.
  useEffect(() => {
    const controlador = new AbortController();
    setRecargando(true);
    reservasPersonalService.listarReservas(armarFiltrosAgenda(aplicados), { signal: controlador.signal })
      .then((resultado) => {
        setItems(Array.isArray(resultado?.items) ? resultado.items : []);
        setTotal(resultado?.total ?? 0);
        setActualizadoA(horaActualSantoDomingo());
        setErrorLista('');
        setFase('listo');
        setRecargando(false);
        setCargas((n) => n + 1);
      })
      .catch((error) => {
        if (esPeticionCancelada(error)) return;
        setErrorLista(mensajeDeError(error));
        setFase('error');
        setRecargando(false);
      });
    return () => controlador.abort();
  }, [aplicados, version]);

  // Código: se pide al cambiar `codigoAplicado` (ya con la espera de 300 ms) o al reintentar. Cambiar el código
  // cancela la petición anterior (AbortController) y una respuesta vieja nunca pisa a la nueva.
  useEffect(() => {
    if (codigoAplicado === '') {
      setResultadoCodigo((actual) => (actual.estado === 'idle' ? actual : RESULTADO_CODIGO_INICIAL));
      return undefined;
    }
    const controlador = new AbortController();
    setResultadoCodigo({ estado: 'cargando', fila: null });
    reservasPersonalService.obtenerReservaPorCodigo(codigoAplicado, { signal: controlador.signal })
      .then((detalle) => {
        if (controlador.signal.aborted) return;
        setResultadoCodigo({ estado: 'encontrada', fila: detalleAFila(detalle) });
      })
      .catch((error) => {
        if (esPeticionCancelada(error) || controlador.signal.aborted) return;
        setResultadoCodigo(resultadoDeError(error));
      });
    return () => controlador.abort();
  }, [codigoAplicado, versionCodigo]);

  // Aplica lo clasificado: un código de 8 dígitos o un teléfono válido se buscan; cualquier otra cosa (vacío,
  // incompleto, inválido) quita ambas búsquedas y la agenda vuelve a ser la del día. Solo se cambia el estado si
  // algo cambia (si no, cada tecla volvería a pedir la lista). `forzar` (Enter o «Buscar») repite la búsqueda aunque
  // el valor sea el mismo.
  const aplicarBusqueda = useCallback((clasif, forzar = false) => {
    const telefono = clasif.tipo === 'telefono' ? clasif.digitos : '';
    const codigo = clasif.tipo === 'codigo' ? clasif.digitos : '';
    setAplicados((actuales) => (actuales.telefono === telefono ? actuales : { ...actuales, telefono, page: 1 }));
    setCodigoAplicado((actual) => (actual === codigo ? actual : codigo));
    if (forzar && telefono) setVersion((v) => v + 1);
    if (forzar && codigo) setVersionCodigo((v) => v + 1);
  }, []);

  // Búsqueda automática: 300 ms después de la última tecla.
  useEffect(() => {
    const temporizador = setTimeout(() => aplicarBusqueda(clasificarBusqueda(busqueda)), DEMORA_BUSQUEDA_MS);
    return () => clearTimeout(temporizador);
  }, [busqueda, aplicarBusqueda]);

  const cambiarFecha = useCallback((fecha) => {
    setResaltado('');
    setAplicados((actuales) => ({ ...actuales, fecha, page: 1 }));
  }, []);
  const cambiarEstado = useCallback((estado) => {
    setResaltado('');
    setAplicados((actuales) => ({ ...actuales, estado, page: 1 }));
  }, []);
  const cambiarFranja = useCallback((franjaId) => {
    setResaltado('');
    setAplicados((actuales) => ({ ...actuales, franjaId, page: 1 }));
  }, []);
  const irAPagina = useCallback((page) => {
    setResaltado('');
    setAplicados((actuales) => ({ ...actuales, page }));
  }, []);

  // «Ver en la agenda» tras crear una reserva: fecha de la reserva, sin otros filtros, y su fila resaltada.
  const irAReserva = useCallback((fecha, codigo) => {
    setBusqueda('');
    setCodigoAplicado('');
    setAplicados({ fecha, estado: '', franjaId: '', telefono: '', page: 1 });
    setResaltado(codigo);
  }, []);

  // Lo que se escribe solo se guarda en el estado de la página; la búsqueda automática la dispara el efecto de arriba.
  // (El resaltado de la reserva recién creada se quita al escribir, no en la búsqueda automática de 300 ms después.)
  const escribirBusqueda = useCallback((texto) => {
    setResaltado('');
    setBusqueda(texto);
  }, []);

  // Enter o «Buscar»: busca de inmediato lo que haya (código o teléfono válido). Con el campo vacío, quita la
  // búsqueda. Si lo escrito está incompleto o es inválido, no busca y el campo ya muestra el porqué.
  const buscarAhora = useCallback(() => {
    setResaltado('');
    aplicarBusqueda(clasificarBusqueda(busqueda), true);
  }, [busqueda, aplicarBusqueda]);

  const reintentarCodigo = useCallback(() => setVersionCodigo((v) => v + 1), []);

  const limpiar = useCallback(() => {
    setBusqueda('');
    setCodigoAplicado('');
    setResaltado('');
    setAplicados(filtrosIniciales());
  }, []);

  // «Actualizar» y «la reserva ya no existe» recargan la lista y, si hay una búsqueda por código, también esa reserva.
  const actualizar = useCallback(() => {
    setVersion((v) => v + 1);
    setVersionCodigo((v) => v + 1);
  }, []);

  // Tras una acción (check-in, cancelar, completar...): la respuesta del servidor es el detalle; se actualiza esa
  // fila en la lista del día y también la de la reserva encontrada por código, si es la misma. Si la fila ya no
  // está en la página (por ejemplo tras un refresco), no hace nada.
  const reemplazarFila = useCallback((detalle) => {
    const fila = detalleAFila(detalle);
    setItems((actuales) => actuales.map((item) => (item.id === fila.id ? fila : item)));
    setResultadoCodigo((actual) => (
      actual.estado === 'encontrada' && actual.fila.id === fila.id ? { ...actual, fila } : actual
    ));
  }, []);

  return {
    aplicados,
    busqueda,
    clasificacion,
    resultadoCodigo,
    items,
    total,
    fase,
    recargando,
    errorLista,
    actualizadoA,
    franjas,
    franjasFallaron,
    franjasEstado,
    franjasError,
    reintentarFranjas,
    cambiarFecha,
    cambiarEstado,
    cambiarFranja,
    irAPagina,
    irAReserva,
    resaltado,
    cargas,
    escribirBusqueda,
    buscarAhora,
    reintentarCodigo,
    limpiar,
    actualizar,
    reemplazarFila,
  };
}
