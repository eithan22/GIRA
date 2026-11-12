import { useCallback, useEffect, useState } from 'react';
import {
  armarFiltrosAgenda,
  detalleAFila,
  horaActualSantoDomingo,
  hoyEnSantoDomingo,
  validarTelefono,
} from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

// Estado de la agenda. El teléfono vive SOLO aquí (estado de React): no se escribe en la URL,
// en storage ni en la consola, y los errores se muestran sin volcar la petición.

function filtrosIniciales() {
  return { fecha: hoyEnSantoDomingo(), estado: '', franjaId: '', telefono: '', page: 1 };
}

export function useAgendaReservas() {
  // `aplicados` son los filtros con los que se pidió la lista; `telefonoEscrito` es lo que hay en el campo.
  const [aplicados, setAplicados] = useState(filtrosIniciales);
  const [telefonoEscrito, setTelefonoEscrito] = useState('');
  const [errorTelefono, setErrorTelefono] = useState('');

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [fase, setFase] = useState('cargando'); // 'cargando' | 'listo' | 'error'
  const [recargando, setRecargando] = useState(false);
  const [errorLista, setErrorLista] = useState('');
  const [actualizadoA, setActualizadoA] = useState('');
  const [version, setVersion] = useState(0);
  // Cuenta las cargas completadas (no las canceladas): la página lo usa para enfocar una fila tras cargar.
  const [cargas, setCargas] = useState(0);

  const [franjas, setFranjas] = useState([]);
  const [franjasFallaron, setFranjasFallaron] = useState(false);

  // Código de la reserva recién creada que la agenda resalta (se quita al cambiar de filtro o página).
  const [resaltado, setResaltado] = useState('');

  // Franjas: una sola vez. Si falla, el filtro por franja se deshabilita y lo demás sigue.
  useEffect(() => {
    const controlador = new AbortController();
    reservasPersonalService.listarFranjasOperativas({ signal: controlador.signal })
      .then((lista) => setFranjas(Array.isArray(lista) ? lista : []))
      .catch((error) => {
        if (!esPeticionCancelada(error)) setFranjasFallaron(true);
      });
    return () => controlador.abort();
  }, []);

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
    setTelefonoEscrito('');
    setErrorTelefono('');
    setAplicados({ fecha, estado: '', franjaId: '', telefono: '', page: 1 });
    setResaltado(codigo);
  }, []);

  const escribirTelefono = useCallback((texto) => {
    setTelefonoEscrito(texto);
    setErrorTelefono('');
  }, []);

  // Con el campo vacío, «Buscar» quita la búsqueda por teléfono y vuelve a la agenda del día.
  const buscarPorTelefono = useCallback(() => {
    setResaltado('');
    if (telefonoEscrito.trim() === '') {
      setErrorTelefono('');
      setAplicados((actuales) => ({ ...actuales, telefono: '', page: 1 }));
      return;
    }
    const validacion = validarTelefono(telefonoEscrito);
    if (!validacion.valido) {
      setErrorTelefono(validacion.mensaje);
      return;
    }
    setErrorTelefono('');
    setAplicados((actuales) => ({ ...actuales, telefono: validacion.digitos, page: 1 }));
  }, [telefonoEscrito]);

  const limpiar = useCallback(() => {
    setTelefonoEscrito('');
    setErrorTelefono('');
    setResaltado('');
    setAplicados(filtrosIniciales());
  }, []);

  const actualizar = useCallback(() => setVersion((v) => v + 1), []);

  // Tras una acción: la respuesta del servidor es el detalle; se actualiza solo esa fila.
  // Si la fila ya no está en la página (por ejemplo tras un refresco), no hace nada.
  const reemplazarFila = useCallback((detalle) => {
    const fila = detalleAFila(detalle);
    setItems((actuales) => actuales.map((item) => (item.id === fila.id ? fila : item)));
  }, []);

  return {
    aplicados,
    telefonoEscrito,
    errorTelefono,
    items,
    total,
    fase,
    recargando,
    errorLista,
    actualizadoA,
    franjas,
    franjasFallaron,
    cambiarFecha,
    cambiarEstado,
    cambiarFranja,
    irAPagina,
    irAReserva,
    resaltado,
    cargas,
    escribirTelefono,
    buscarPorTelefono,
    limpiar,
    actualizar,
    reemplazarFila,
  };
}
