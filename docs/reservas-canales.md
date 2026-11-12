# Canales de reserva y ciclo de vida (GI-403)

Definición para el SRS del módulo Reservas. La antelación mínima (2 h) y la ventana máxima (30 días) son
parámetros provisionales (`ParametrosReservaService`) hasta que Configuración los entregue (GI-226).

## Canales

- **Web.** El cliente reserva por su cuenta desde `/reservar`, sin cuenta. Debe hacerlo con al menos 2 horas
  de antelación y hasta 30 días antes. Origen: `Web`.
- **Por llamada.** El personal registra la reserva de quien llama, desde Agenda → Nueva reserva. Puede ser
  para hoy, sin la antelación de 2 horas, mientras la franja no haya terminado. Origen: `Telefono`.
- **En persona.** El personal registra la reserva con el cliente presente en el restaurante, con las mismas
  reglas que por llamada. Origen: `Presencial`.
- **Sin reserva (visita).** Un grupo que llega y se sienta de inmediato. No es una reserva: no tiene código
  ni cliente. Se registra la franja, las personas y, opcionalmente, la mesa. No marca la mesa como ocupada en
  el mapa, porque no tiene hora de salida y no se sabe cuándo se libera.

## Reglas comunes a las reservas

- **Máximo de personas:** el tamaño de la mesa más grande activa (hoy, 8 personas). Los grupos mayores se
  atienden por teléfono, fuera del sistema.
- **Mesa:** se asigna automáticamente: la mesa libre de menor capacidad que alcance para el grupo en esa fecha
  y franja.
- **Límite por teléfono:** como máximo 2 reservas vigentes con el mismo teléfono.
- **Disponibilidad:** la del personal (`GET /api/reservas/disponibilidad-personal`) incluye la franja de hoy en
  curso y solo excluye las que ya terminaron; la pública (`GET /api/reservas/disponibilidad`) conserva la
  antelación de 2 horas.

## Ciclo de vida de la reserva

1. Se crea **Confirmada**.
2. Con el check-in (desde 30 minutos antes de la franja y hasta que termina) pasa a **EnCurso**, con las
   personas reales que llegaron.
3. **Completada**: al terminar la franja se completa sola, o el personal la completa antes.
4. Desde Confirmada también puede quedar **Cancelada** (el personal, hasta el inicio de la franja; el cliente,
   con al menos 2 horas de anticipación) o **NoAsistio**: si no llega, se marca sola cuando pasan 15 minutos de
   tolerancia, contados desde el mayor entre el inicio de la franja y el momento en que se registró la reserva.

> **Reserva creada con la franja en curso (RN-RES-04).** Como el personal solo puede modificar y cancelar
> hasta el inicio de la franja, una reserva creada con la franja ya en curso no se puede modificar ni cancelar
> después: solo se ajustan las personas reales en el check-in. Si el cliente no llega, el sistema la marca
> NoAsistio a los 15 minutos de su registro.
