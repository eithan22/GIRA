# Parámetros de reserva (GI-226)

El restaurante adapta las reglas de reserva sin tocar código: el sistema se entrega con valores por defecto
sensatos y todos se editan desde **Reservas → Configuración** (pestañas Políticas, Horario y Días cerrados).
Solo el Gerente y el Administrador editan; el resto del personal (`OperarReservas`) puede consultarlos. El cliente
ve, sin autenticarse, solo lo necesario para reservar (`GET /api/reservas/politicas`).

## Las seis políticas

| Parámetro | Qué hace | Por defecto | Rango | A qué reservas aplica |
|---|---|---|---|---|
| Antelación mínima | Con cuánta anticipación se puede reservar por la web (el personal puede reservar sin ella) | 120 min (2 h) | ≥ 0 y menor que la antelación máxima | Reservas **nuevas** |
| Antelación máxima | Hasta cuántos días adelante se puede reservar | 30 días | 1 a 365 | Reservas **nuevas** |
| Límite de cancelación | Hasta cuánto antes del inicio de la franja puede cancelar el cliente | 120 min (2 h) | ≥ 0 y no mayor que la antelación máxima | Se evalúa **al cancelar** (también en reservas ya hechas) |
| Ventana de check-in | Desde cuánto antes del inicio de la franja se permite el check-in | 15 min | 0 a 120 | Se evalúa **al hacer el check-in** |
| Reservas vigentes por teléfono | Máximo de reservas Confirmada o En curso con un mismo teléfono | 2 | 1 a 10 | Reservas **nuevas** |
| Tamaño máximo de grupo | Máximo de personas por reserva | 8 | 1 a 50 | Reservas **nuevas** |

- **Tamaño efectivo de grupo** = el menor entre el máximo configurado y la capacidad de la mesa activa más grande.
- **No hay tolerancia de no-show** (GI-404): una reserva Confirmada sin check-in pasa a No asistió al terminar su
  franja, o el personal la marca antes con «Marcar No asistió» una vez que la franja empezó.
- **«Restablecer valores recomendados»** carga los valores por defecto en el formulario **sin guardarlos**; solo se
  guardan al pulsar Guardar. Los valores por defecto salen de `ParametrosReservaService.ValoresPorDefecto`.

### Reglas de creación y reglas operativas

- Las **reglas de creación** (antelación mínima y máxima, reservas por teléfono, tamaño de grupo, horario y días
  cerrados) rigen para las reservas nuevas y **no invalidan las existentes**.
- Las **reglas operativas** (ventana de check-in y límite de cancelación) se leen en el momento en que se evalúan:
  un cambio rige desde la siguiente acción, también sobre reservas ya hechas.

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
3. **Visitas sin reserva y mesas.** Una visita sin reserva **no bloquea la mesa** en la asignación de una reserva del
   personal: la reserva puede tomar la mesa donde hay una visita en la misma franja. Es una decisión de producto
   pendiente: la visita no tiene hora de salida, y el mapa de ocupación tampoco la cuenta (GI-402).
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
10. **Llegar tarde.** Mientras la reserva siga Confirmada el cliente puede llegar durante toda su franja; una vez No
    asistió pierde la reserva y solo se le puede atender como visita sin reserva (ver `reservas-canales.md`).
