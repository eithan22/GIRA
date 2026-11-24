// Campo de hora con dos listas desplegables (hora y minutos), para elegirla con la barra de desplazamiento.
// El valor es "HH:mm" en formato de 24 horas, igual que el resto del módulo.

const HORAS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0'));
const MINUTOS = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'));

/**
 * @param {{ etiqueta: string, valor: string, onChange: (valor: string) => void }} props
 * Al elegir una parte, la otra se completa con 00. Si se vuelve a la opción vacía, el valor se limpia.
 */
export default function SelectorHora({ etiqueta, valor, onChange }) {
  const [hora, minuto] = valor ? valor.split(':') : ['', ''];

  function cambiar(nuevaHora, nuevoMinuto) {
    if (nuevaHora === '' || nuevoMinuto === '') {
      onChange('');
      return;
    }
    const h = nuevaHora ?? (hora || '00');
    const m = nuevoMinuto ?? (minuto || '00');
    onChange(`${h}:${m}`);
  }

  return (
    <div className="form-field">
      <span>{etiqueta} <i>*</i></span>
      <div className="selector-hora">
        <div className="select-wrap">
          <select aria-label={`${etiqueta}: hora`} value={hora} onChange={(e) => cambiar(e.target.value, undefined)}>
            <option value="">Hora</option>
            {HORAS.map((h) => <option key={h} value={h}>{h}</option>)}
          </select>
        </div>
        <span className="selector-hora-separador" aria-hidden="true">:</span>
        <div className="select-wrap">
          <select aria-label={`${etiqueta}: minutos`} value={minuto} onChange={(e) => cambiar(undefined, e.target.value)}>
            <option value="">Min</option>
            {MINUTOS.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
      </div>
    </div>
  );
}
