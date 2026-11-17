import { TEXTO_CAMBIA } from '../reservasPersonalTextos.js';

/**
 * Tabla antes / después de una modificación. Las filas que cambian llevan el texto «cambia»
 * además del énfasis visual: el cambio nunca depende solo del color.
 */
export default function ResumenCambios({ filas }) {
  return (
    <table className="reservas-personal-tabla-cambios">
      <caption className="reservas-solo-lectores">Cambios de la reserva</caption>
      <thead>
        <tr>
          <th scope="col"><span className="reservas-solo-lectores">Dato</span></th>
          <th scope="col">Antes</th>
          <th scope="col">Después</th>
        </tr>
      </thead>
      <tbody>
        {filas.map((fila) => (
          <tr key={fila.clave} className={fila.cambia ? 'reservas-personal-fila-cambia' : undefined}>
            <th scope="row">{fila.etiqueta}</th>
            <td>{fila.antes}</td>
            <td>
              {fila.despues}
              {fila.cambia && <span className="reservas-personal-marca-cambia"> ({TEXTO_CAMBIA})</span>}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
