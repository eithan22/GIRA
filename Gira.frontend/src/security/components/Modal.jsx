import Icon from './Icon.jsx';

export default function Modal({ title, children, onClose, wide = false }) {
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className={`modal-card${wide ? ' modal-wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <button className="icon-button modal-close" type="button" onClick={onClose} aria-label="Cerrar"><Icon name="close" size={18} /></button>
        <div className="modal-content"><span className="eyebrow">GIRA · SEGURIDAD</span><h2>{title}</h2>{children}</div>
      </section>
    </div>
  );
}
