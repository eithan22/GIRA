/**
 * Lista de zonas, mesas o franjas: tabla en escritorio y tablet y tarjetas en móvil (≤720 px), sin
 * scroll horizontal (reutiliza las clases de la agenda). `columnas`: [{ clave, titulo, render(fila) }];
 * la primera columna es el título de la tarjeta. `acciones(fila)` devuelve los botones de la fila y
 * `atenuada(fila)` marca las filas inactivas.
 */
export default function TablaSalon({ titulo, columnas, filas, acciones, atenuada }) {
  const [principal, ...resto] = columnas;

  return (
    <div className="reservas-personal-lista">
      <div className="reservas-personal-tabla-envoltura">
        <table className="reservas-personal-tabla">
          <caption className="reservas-solo-lectores">{titulo}</caption>
          <thead>
            <tr>
              {columnas.map((columna) => <th key={columna.clave} scope="col">{columna.titulo}</th>)}
              <th scope="col"><span className="reservas-solo-lectores">Acciones</span></th>
            </tr>
          </thead>
          <tbody>
            {filas.map((fila) => (
              <tr key={fila.id} className={atenuada?.(fila) ? 'reservas-personal-fila-inactiva' : undefined}>
                {columnas.map((columna) => <td key={columna.clave}>{columna.render(fila)}</td>)}
                <td className="reservas-personal-celda-accion">{acciones(fila)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="reservas-personal-tarjetas">
        {filas.map((fila) => (
          <li
            key={fila.id}
            className={`reservas-personal-tarjeta-reserva${atenuada?.(fila) ? ' reservas-personal-fila-inactiva' : ''}`}
          >
            <strong className="reservas-personal-ajustable">{principal.render(fila)}</strong>
            <dl className="reservas-personal-tarjeta-datos">
              {resto.map((columna) => (
                <div key={columna.clave}>
                  <dt>{columna.titulo}</dt>
                  <dd>{columna.render(fila)}</dd>
                </div>
              ))}
            </dl>
            <div className="reservas-personal-tarjeta-acciones">{acciones(fila)}</div>
          </li>
        ))}
      </ul>
    </div>
  );
}
