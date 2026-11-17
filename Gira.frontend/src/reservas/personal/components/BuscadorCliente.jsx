import { useEffect, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearTelefono } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AYUDA_BUSCAR_CLIENTE,
  ETIQUETA_BUSCAR_CLIENTE,
  TEXTO_BUSCANDO_CLIENTES,
  TEXTO_SIN_CLIENTES,
  TEXTO_USAR_CLIENTE,
} from '../reservasPersonalTextos.js';

const MINIMO_CARACTERES = 3;
const DEMORA_MS = 350;
const MAXIMO_RESULTADOS = 5;

/**
 * Búsqueda de un cliente existente (GET /api/clientes?buscar=) con lista de botones «Usar», no un
 * combobox. Mínimo 3 caracteres, espera de 350 ms y cancelación de la consulta anterior. El texto
 * escrito vive solo aquí (estado del componente); viaja en el query string bajo la excepción
 * documentada en docs/proteger-endpoints.md y no se guarda en ningún otro sitio.
 */
export default function BuscadorCliente({ onUsar, deshabilitado }) {
  const [texto, setTexto] = useState('');
  const [estado, setEstado] = useState('idle'); // 'idle' | 'cargando' | 'listo' | 'error'
  const [resultados, setResultados] = useState([]);
  const [error, setError] = useState('');

  const buscado = texto.trim();

  useEffect(() => {
    if (buscado.length < MINIMO_CARACTERES) {
      setEstado('idle');
      setResultados([]);
      setError('');
      return undefined;
    }

    const controlador = new AbortController();
    setEstado('cargando');
    setError('');
    const temporizador = setTimeout(() => {
      reservasPersonalService.buscarClientes({ buscar: buscado, pageSize: MAXIMO_RESULTADOS }, { signal: controlador.signal })
        .then((pagina) => {
          setResultados(Array.isArray(pagina?.items) ? pagina.items : []);
          setEstado('listo');
        })
        .catch((fallo) => {
          if (esPeticionCancelada(fallo)) return;
          setError(mensajeDeError(fallo));
          setEstado('error');
        });
    }, DEMORA_MS);

    return () => {
      clearTimeout(temporizador);
      controlador.abort();
    };
  }, [buscado]);

  function usar(cliente) {
    onUsar(cliente);
    setTexto('');
  }

  let mensaje = '';
  if (estado === 'cargando') mensaje = TEXTO_BUSCANDO_CLIENTES;
  else if (estado === 'listo' && resultados.length === 0) mensaje = TEXTO_SIN_CLIENTES;
  else if (estado === 'listo') mensaje = resultados.length === 1 ? '1 cliente encontrado' : `${resultados.length} clientes encontrados`;

  return (
    <div className="reservas-campo">
      <label className="reservas-campo-etiqueta" htmlFor="nueva-buscar-cliente">{ETIQUETA_BUSCAR_CLIENTE}</label>
      <input
        id="nueva-buscar-cliente"
        type="search"
        autoComplete="off"
        className="reservas-input"
        value={texto}
        onChange={(evento) => setTexto(evento.target.value)}
        disabled={deshabilitado}
        aria-describedby="nueva-buscar-cliente-ayuda"
      />
      <span id="nueva-buscar-cliente-ayuda" className="reservas-campo-ayuda">{AYUDA_BUSCAR_CLIENTE}</span>
      <p className="reservas-personal-estado-franjas" role="status" aria-live="polite">{mensaje}</p>

      {estado === 'error' && <Aviso tipo="error">{error}</Aviso>}

      {estado === 'listo' && resultados.length > 0 && (
        <ul className="reservas-personal-clientes">
          {resultados.map((cliente) => (
            <li key={cliente.id}>
              <span className="reservas-personal-ajustable">
                <strong>{cliente.nombre}</strong>
                <span className="reservas-personal-secundario">{formatearTelefono(cliente.telefono)}</span>
              </span>
              <button
                type="button"
                className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
                onClick={() => usar(cliente)}
                aria-label={`Usar a ${cliente.nombre}`}
              >{TEXTO_USAR_CLIENTE}</button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
