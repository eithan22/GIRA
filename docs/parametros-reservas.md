# Parámetros de reserva (GI-226)

El restaurante adapta las reglas de reserva sin tocar código: el sistema se entrega con valores por defecto
sensatos y todos se editan desde **Reservas → Configuración** (pestañas Políticas, Horario y Días cerrados).
Solo el Gerente y el Administrador editan; el resto del personal (`OperarReservas`) puede consultarlos. El cliente
ve, sin autenticarse, solo lo necesario para reservar (`GET /api/reservas/politicas`).

## Las ocho políticas

| Parámetro | Qué hace | Por defecto | Rango | A qué reservas aplica |
|---|---|---|---|---|
| Antelación mínima | Con cuánta anticipación se puede reservar por la web (el personal puede reservar sin ella) | 120 min (2 h) | ≥ 0 y menor que la antelación máxima | Reservas **nuevas** |
| Antelación máxima | Hasta cuántos días adelante se puede reservar | 30 días | 1 a 365 | Reservas **nuevas** |
| Límite de cancelación | Hasta cuánto antes del inicio de la franja puede cancelar el cliente | 120 min (2 h) | ≥ 0 y no mayor que la antelación máxima | Se evalúa **al cancelar** (también en reservas ya hechas) |
| Ventana de check-in | Desde cuánto antes del inicio de la franja se permite el check-in | 15 min | 0 a 120 | Se evalúa **al hacer el check-in** |
| Tolerancia de no-show | Cuánto se espera al cliente antes de marcar No asistió | 15 min | 0 a 120, y menor que la franja activa más corta | Se evalúa **en cada ciclo** del proceso automático (también en reservas ya hechas) |
| Reservas vigentes por teléfono | Máximo de reservas Confirmada o En curso con un mismo teléfono | 2 | 1 a 10 | Reservas **nuevas** |
| Tamaño máximo de grupo | Máximo de personas por reserva | 8 | 1 a 50 | Reservas **nuevas** |
| Teléfono de contacto | Teléfono que se muestra al cliente en `/reservar` (grupos grandes, dudas) | *vacío* | Hasta 30 caracteres: dígitos, espacios, `+`, `-`, `( )` y `.` | No aplica (solo informativo) |

- **Tamaño efectivo de grupo** = el menor entre el máximo configurado y la capacidad de la mesa activa más grande.
- **Tolerancia de no-show** (GI-404, editable, 15 minutos por defecto): una reserva Confirmada sin check-in pasa a
  No asistió a los N minutos del inicio de su franja (N = tolerancia configurada, contada desde el mayor entre el
  inicio de la franja y el último registro de la reserva), o el personal la marca antes con «Marcar No asistió» una
  vez que la franja empezó. La tolerancia debe ser **menor que la duración de la franja activa más corta**: si no, el
  servidor responde 409 nombrando esa franja. Por el mismo motivo, al crear, acortar o reactivar una franja activa su
  duración debe superar la tolerancia vigente (409 con la duración y la tolerancia en el mensaje).
- **Teléfono de contacto**: se entrega **vacío** (no hay número de ejemplo). Mientras no se configure, el endpoint
  público lo devuelve como `null`. Un valor en blanco se guarda como vacío, no como cadena vacía.
- **«Restablecer valores recomendados»** carga los valores por defecto en el formulario **sin guardarlos**; solo se
  guardan al pulsar Guardar. Los valores por defecto salen de `ParametrosReservaService.ValoresPorDefecto`; el
  teléfono de contacto no tiene valor recomendado (se restablece vacío).

### Reglas de creación y reglas operativas

- Las **reglas de creación** (antelación mínima y máxima, reservas por teléfono, tamaño de grupo, horario y días
  cerrados) rigen para las reservas nuevas y **no invalidan las existentes**.
- Las **reglas operativas** (ventana de check-in, límite de cancelación y tolerancia de no-show) se leen en el
  momento en que se evalúan: un cambio rige desde la siguiente acción —o desde el siguiente ciclo del proceso
  automático, en el caso de la tolerancia—, también sobre reservas ya hechas.

## Horario de operación

Una fila por día de la semana: abierto o cerrado, hora de apertura y hora de cierre (con el apertura < cierre).
Por defecto, los siete días de 12:00 a 22:30. En la API, el día es el `DayOfWeek` de .NET (0 = domingo … 6 = sábado);
la pantalla los muestra de lunes a domingo.

**Las franjas activas deben caber en el horario de todos los días abiertos.** Al crear, cambiar el horario o reactivar
una franja que no cabe, el servidor responde 409 con el día y el horario. Al reducir el horario de un día, no puede
quedar fuera ninguna franja activa (el error indica cuál). Cerrar un día completo siempre se puede.

## Días cerrados

Fechas concretas en las que el restaurante no abre (feriados, eventos), con un motivo opcional. No se pueden cerrar
fechas pasadas ni repetir una fecha ya cerrada. Al cerrar una fecha, el sistema avisa cuántas reservas vigentes hay
ese día; **no las cancela**.

## Franjas por defecto

Almuerzo 12:00–13:45, Tarde 14:00–15:45, Cena 18:30–20:15 y Noche 20:30–22:15 (15 minutos entre las contiguas, todas
dentro de 12:00–22:30). Se crean **solo si no existen** y nunca pisan lo que el Gerente haya editado.

## Limitaciones conocidas

1. **Mesas de más de 8 personas.** Una instalación con mesas de más de 8 personas queda limitada a 8 por reserva
   hasta que se edite el parámetro de tamaño máximo de grupo (el efectivo es el menor entre el parámetro y la mesa más
   grande). Si necesitan grupos mayores, hay que subir el parámetro.
2. **Instalaciones existentes y franjas.** Las instalaciones existentes **conservan sus franjas anteriores**: las 4
   franjas de 2 horas solo se insertan en una base vacía o con las 2 franjas originales (Almuerzo 12:00–15:00 y Cena
   18:00–22:30) sin tocar y sin reservas ni visitas que las usen (la migración `Reservas_FranjasDeDosHoras`). Si hay
   reservas, visitas o ediciones del Gerente, no se cambia nada y el Gerente ajusta las franjas desde Configuración.
3. **Visitas sin reserva y mesas.** Una visita sin reserva **sí bloquea la mesa** en su fecha y franja: la asignación
   automática la salta, la disponibilidad (pública y del personal) no ofrece la franja si no queda otra mesa que
   alcance, y la mesa elegida a mano se rechaza con 409 («tiene una visita sin reserva en esta franja»). Como la
   visita no tiene hora de salida, se considera ocupada **toda la franja** y se libera al cambiar de franja. Se
   evaluó darle estado u hora de fin y se decidió no hacerlo: es un registro ya consumado, no una sesión con ciclo de
   vida. El **mapa de ocupación** no la pinta Ocupada ni la cuenta en el resumen (GI-402), pero sí muestra la mesa
   con la etiqueta «Con visita», sin cambiar su estado.
4. **Reducir el horario o cerrar un día no cancela reservas ya hechas.** Solo impide las reservas nuevas; las
   existentes se atienden normalmente.
5. **Las reglas de creación no invalidan las reservas existentes.** Una reserva creada con una antelación o un tamaño de
   grupo anteriores sigue siendo válida.
6. **Caché.** Los parámetros se guardan en memoria hasta 2 minutos y se invalidan al guardar; con varias instancias de
   la API, un cambio puede tardar hasta 2 minutos en verse en las demás.
7. **Concurrencia.** El 409 por modificación concurrente detecta dos guardados simultáneos; una pantalla abierta mucho
   rato que guarda sobre cambios ajenos ya hechos los sobrescribe (el cliente no envía la versión de la fila).
8. **Franjas existentes y horario.** Las franjas ya creadas no se revalidan cuando el horario cambia después; se validan
   al crearlas, al cambiar su horario o al reactivarlas.
9. **Días de la semana cerrados.** El endpoint público de políticas lista solo las fechas cerradas dentro de la ventana
   de reserva; un día de la semana cerrado se descubre por la disponibilidad (todas las franjas salen con el motivo
   «El restaurante está cerrado ese día.»).
10. **Llegar tarde.** Mientras la reserva siga Confirmada el cliente puede llegar durante toda su franja, pero el
    proceso automático la marca No asistió al vencer la tolerancia configurada; una vez No asistió pierde la reserva
    y solo se le puede atender como visita sin reserva (ver `reservas-canales.md`).
11. **Sin parámetros contra reservas falsas.** Las políticas siguen siendo las ocho de arriba. El teléfono solo se
    valida por formato y la única barrera, además de la limitación de tasa, es el máximo de reservas vigentes por
    teléfono. El límite diario por IP y el bloqueo por no-show están aplazados como mejora futura (ver
    `reservas-canales.md`, «Mejoras futuras»).

## Reglas relacionadas que no son parámetros (RN-RES-04)

Estas reglas son fijas, no configurables desde Políticas; se documentan aquí porque dependen del inicio de la
franja igual que la ventana de check-in y la tolerancia de no-show. Detalle completo en `reservas-canales.md`.

- **Aviso de próxima reserva al registrar una visita.** Solo avisa de la franja activa **inmediatamente siguiente**
  a aquella en la que se registra la visita (no de cualquier franja posterior del día): con el tiempo
  mostrado como «M min» (menos de 60), «H h» (hora exacta) o «H h M min».
- **`puedeModificar` / `puedeCancelar`.** El detalle de una reserva para el personal indica si todavía se puede
  modificar o cancelar (Confirmada y franja sin empezar, con el reloj del servidor); la agenda deshabilita esos
  botones cuando el backend responde `false`, en vez de mostrarlos activos y fallar solo al enviarlos.
- **`zonaId` por el canal del personal.** El canal del personal (`Telefono`/`Presencial`) ya no ignora `zonaId` en
  silencio: lo rechaza con 400 y el código `ZONA_NO_APLICA_PERSONAL`, porque ese canal asigna la mesa a mano.
