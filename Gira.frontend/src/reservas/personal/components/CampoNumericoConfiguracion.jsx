/**
 * Campo numérico de Configuración con etiqueta visible, unidad visible, el texto legible al lado ("= 2 horas") y
 * ayuda. La rueda del ratón NO cambia el valor (se quita el foco al girarla sobre el campo). El error del campo se
 * enlaza con aria-describedby y aria-invalid.
 */
export default function CampoNumericoConfiguracion({
  id, etiqueta, unidad, legible, ayuda, valor, minimo, maximo, error, onCambiar, inputRef,
}) {
  const describedBy = [ayuda ? `${id}-ayuda` : null, legible ? `${id}-legible` : null, error ? `${id}-error` : null]
    .filter(Boolean).join(' ');

  return (
    <div className="reservas-campo">
      <label className="reservas-campo-etiqueta" htmlFor={id}>{etiqueta}</label>
      <div className="reservas-personal-campo-unidad">
        <input
          id={id}
          ref={inputRef}
          type="number"
          inputMode="numeric"
          min={minimo}
          max={maximo}
          step={1}
          autoComplete="off"
          className="reservas-input reservas-personal-input-numero"
          value={valor}
          onChange={(evento) => onCambiar(evento.target.value)}
          onWheel={(evento) => evento.currentTarget.blur()}
          aria-invalid={error ? 'true' : undefined}
          aria-describedby={describedBy || undefined}
        />
        <span className="reservas-personal-unidad">{unidad}</span>
        {legible && <span id={`${id}-legible`} className="reservas-personal-legible">{legible}</span>}
      </div>
      {ayuda && <span id={`${id}-ayuda`} className="reservas-campo-ayuda">{ayuda}</span>}
      {error && <span id={`${id}-error`} className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{error}</span>}
    </div>
  );
}
