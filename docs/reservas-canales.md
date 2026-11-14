# Canales de reserva y ciclo de vida (GI-403, GI-226)

Definición para el SRS del módulo Reservas. Las reglas de reserva (antelaciones, límites y tamaño de grupo) son
**parámetros editables** por el Gerente y el Administrador desde Configuración (GI-226); ver
[parametros-reservas.md](parametros-reservas.md). Los valores que se citan abajo son los **por defecto**.

## Franjas por defecto

El sistema se entrega con cuatro franjas de 2 horas: **Almuerzo 12:00–13:45, Tarde 14:00–15:45, Cena 18:30–20:15 y
Noche 20:30–22:15**, con 15 minutos entre las contiguas y dentro del horario de operación (12:00–22:30). Una franja
activa debe caber en el horario de todos los días abiertos. Las franjas se crean solo si no existen: las instalaciones
existentes conservan las suyas hasta que el Gerente las edite.

## Canales

- **Web.** El cliente reserva por su cuenta desde `/reservar`, sin cuenta. Debe hacerlo con al menos la antelación
  mínima (por defecto 2 horas) y hasta la antelación máxima (por defecto 30 días). `/reservar` muestra **todas** las
  franjas del día: las que no se pueden elegir salen deshabilitadas con su motivo. Origen: `Web`.
- **Por llamada.** El personal registra la reserva de quien llama, desde Agenda → Nueva reserva. Puede ser
  para hoy, sin la antelación mínima, mientras la franja no haya terminado. Origen: `Telefono`.
- **En persona.** El personal registra la reserva con el cliente presente en el restaurante, con las mismas
  reglas que por llamada. Origen: `Presencial`.
- **Sin reserva (visita).** Un grupo que llega y se sienta de inmediato. No es una reserva: no tiene código
  ni cliente. Se registra la franja, las personas y, opcionalmente, la mesa; no se guardan datos personales.
  **No marca la mesa como ocupada en el mapa**, porque no tiene hora de salida y no se sabe cuándo se libera.
  Las visitas del día se consultan en la sección «Visitas sin reserva de hoy» de la Agenda (total de visitas y
  de personas, y la lista con las más recientes primero; `GET /api/visitas`, hasta 200 por día en la lista).

## Reglas comunes a las reservas

- **Máximo de personas:** el **menor** entre el parámetro de tamaño máximo de grupo (por defecto 8) y la capacidad de
  la mesa activa más grande. Los grupos mayores se atienden por teléfono, fuera del sistema.
- **Mesa:** se asigna automáticamente: la mesa libre de menor capacidad que alcance para el grupo en esa fecha
  y franja. Una visita sin reserva no cuenta para esta asignación (decisión de producto pendiente).
- **Límite por teléfono:** como máximo el número configurado de reservas vigentes con el mismo teléfono (por defecto 2).
- **Día cerrado:** no se puede reservar en un día de la semana cerrado ni en una fecha cerrada, por ningún canal.
- **Disponibilidad:** la pública (`GET /api/reservas/disponibilidad`) devuelve todas las franjas activas con `disponible` y
  `motivoNoDisponible` (cerrado, fuera del rango, ya pasó, antelación mínima, sin mesas); la del personal
  (`GET /api/reservas/disponibilidad-personal`) incluye la franja de hoy en curso, solo excluye las que ya terminaron y
  no exige la antelación mínima.
- **Reglas de creación y reglas operativas.** Las de creación (antelaciones, límite por teléfono, tamaño de grupo,
  horario y días cerrados) rigen para las reservas **nuevas** y no invalidan las existentes. La ventana de check-in y el
  límite de cancelación se leen en el momento de evaluarlos.

## Ciclo de vida de la reserva

1. Se crea **Confirmada**.
2. Con el check-in (desde la ventana configurada antes de la franja, por defecto 15 minutos, y hasta que termina) pasa a
   **EnCurso**, con las personas reales que llegaron.
3. **Completada**: al terminar la franja se completa sola, o el personal la completa antes.
4. Desde Confirmada también puede quedar **Cancelada** (el personal, hasta el inicio de la franja; el cliente,
   con al menos el límite de cancelación configurado, por defecto 2 horas) o **NoAsistio** (GI-404): si el cliente no
   llega, la reserva se marca sola **al terminar la franja** (sin tolerancia), o el personal la marca antes con
   **«Marcar No asistió»** desde el detalle de la reserva, una vez que la franja ya empezó (antes del inicio, el
   personal cancela en vez de marcar). Al quedar NoAsistio la mesa se libera, se publica `ReservaNoAsistidaEvent` y el
   check-in ya no es posible: si el cliente llega después, se registra como visita sin reserva.

> **Reserva creada con la franja en curso (RN-RES-04).** Como el personal solo puede modificar y cancelar
> hasta el inicio de la franja, una reserva creada con la franja ya en curso no se puede modificar ni cancelar
> después: solo se ajustan las personas reales en el check-in. Si el cliente no llega, el sistema la marca
> NoAsistio al terminar la franja, o el personal la marca antes con «Marcar No asistió».

> **Limitación conocida: llegar tarde.** Mientras la reserva siga Confirmada, el cliente puede llegar durante
> toda su franja y se le hace el check-in con normalidad (desde la ventana de check-in y hasta que la franja
> termina). Pero una vez que la reserva pasa a NoAsistio (sola, al terminar la franja, o porque el personal la
> marcó), ya no hay vuelta atrás: el cliente que llega después pierde la reserva y solo se le puede atender
> como visita sin reserva, si hay una mesa libre. Por eso el personal debe marcar «No asistió» únicamente
> cuando esté seguro de que el cliente no vendrá.

> **Limitaciones de la configuración (GI-226).** Una instalación con mesas de más de 8 personas queda limitada a 8
> hasta que se edite el parámetro de tamaño máximo de grupo. Las instalaciones existentes conservan sus franjas
> anteriores. Reducir el horario o cerrar un día no cancela las reservas ya hechas. Detalle en
> [parametros-reservas.md](parametros-reservas.md).
