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
  franjas del día: las que no se pueden elegir salen deshabilitadas con su motivo. Puede elegir una **zona** opcional
  (ver «Zona opcional en la reserva web»). Origen: `Web`.
- **Por llamada.** El personal registra la reserva de quien llama, desde Agenda → Nueva reserva. Puede ser
  para hoy, sin la antelación mínima, mientras la franja no haya terminado. Origen: `Telefono`.
- **En persona.** El personal registra la reserva con el cliente presente en el restaurante, con las mismas
  reglas que por llamada. Origen: `Presencial`.
- **Sin reserva (visita).** Un grupo que llega y se sienta de inmediato. No es una reserva: no tiene código
  ni cliente. Se registra la franja, las personas y, opcionalmente, la mesa; no se guardan datos personales.
  **No marca la mesa como ocupada en el mapa**, porque no tiene hora de salida y no se sabe cuándo se libera; sí
  ocupa esa mesa en esa franja para las reservas y la disponibilidad (ver «Reglas comunes»).
  Las visitas del día se consultan en la sección «Visitas sin reserva de hoy» de la Agenda (total de visitas y
  de personas, y la lista con las más recientes primero; `GET /api/visitas`, hasta 200 por día en la lista).
  **Cuándo se puede registrar (`POST /api/visitas`, hora de Santo Domingo):**
  1. **Solo con el restaurante abierto ahora.** No se registran visitas si el día de la semana o la fecha de hoy están
     cerrados, ni antes de la apertura ni a partir del cierre del horario de hoy (la apertura es inclusiva y el
     cierre exclusivo, como el fin de una franja). Responde 409 con el horario del día.
  2. **Solo en la franja más reciente de las activas que ya empezaron:** la actual o, si el grupo llega entre
     franjas (por ejemplo a las 16:30), la última que empezó. Una franja anterior responde 409 y el mensaje nombra la
     franja correcta; una que todavía no empieza también responde 409. En la pantalla solo esa franja es elegible y
     las demás salen deshabilitadas («Ya terminó», «Todavía no empieza»); con el restaurante cerrado se avisa y el
     botón «Registrar visita» queda deshabilitado.
  El aviso de próxima reserva (`GET /api/visitas/advertencia-mesa`) no cambia: no consulta el horario, porque con el
  restaurante cerrado la visita no llega a registrarse.

## Reglas comunes a las reservas

- **Máximo de personas:** el **menor** entre el parámetro de tamaño máximo de grupo (por defecto 8) y la capacidad de
  la mesa activa más grande. Los grupos mayores se atienden por teléfono, fuera del sistema.
- **Mesa:** se asigna automáticamente: la mesa libre de menor capacidad que alcance para el grupo en esa fecha
  y franja. Una mesa con **visita sin reserva** en esa fecha y franja cuenta como ocupada: la asignación automática
  la salta, la disponibilidad no ofrece la franja si no queda otra mesa que alcance, y si el personal la elige a mano
  recibe 409 («tiene una visita sin reserva en esta franja»). Al no haber hora de salida, se considera ocupada toda
  la franja y se libera al cambiar de franja. El inverso ya existía: no se puede registrar una visita en una mesa con
  reserva vigente en esa franja. En la web el cliente puede acotar la búsqueda a una zona (ver «Zona opcional en la
  reserva web»); el personal no.
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
   llega, la reserva se marca sola **a los N minutos del inicio de su franja** (N = tolerancia de no-show
   configurable, 15 minutos por defecto, contada desde el mayor entre el inicio de la franja y el último registro de
   la reserva), o el personal la marca antes con **«Marcar No asistió»** desde el detalle de la reserva, una vez que
   la franja ya empezó (antes del inicio, el personal cancela en vez de marcar). Al quedar NoAsistio la mesa se
   libera, se publica `ReservaNoAsistidaEvent` y el check-in ya no es posible: si el cliente llega después, se
   registra como visita sin reserva.

> **Reserva creada con la franja en curso (RN-RES-04).** Como el personal solo puede modificar y cancelar
> hasta el inicio de la franja, una reserva creada con la franja ya en curso no se puede modificar ni cancelar
> después: solo se ajustan las personas reales en el check-in. Si el cliente no llega, el sistema la marca
> NoAsistio a los N minutos de su registro (tolerancia configurable: una reserva creada con la franja ya empezada
> tiene su plazo completo desde que se creó), o el personal la marca antes con «Marcar No asistió».

> **Limitación conocida: llegar tarde.** Mientras la reserva siga Confirmada, el cliente puede llegar y se le hace
> el check-in con normalidad (desde la ventana de check-in y hasta que la franja termina). Pero una vez que la
> reserva pasa a NoAsistio (sola, a los N minutos del inicio de su franja según la tolerancia configurada, o porque
> el personal la marcó), ya no hay vuelta atrás: el cliente que llega después pierde la reserva y solo se le puede
> atender como visita sin reserva, si hay una mesa libre. Por eso el personal debe marcar «No asistió» únicamente
> cuando esté seguro de que el cliente no vendrá, y la tolerancia debe ajustarse a lo que el restaurante quiera
> esperar.

> **Limitaciones de la configuración (GI-226).** Una instalación con mesas de más de 8 personas queda limitada a 8
> hasta que se edite el parámetro de tamaño máximo de grupo. Las instalaciones existentes conservan sus franjas
> anteriores. Reducir el horario o cerrar un día no cancela las reservas ya hechas. Detalle en
> [parametros-reservas.md](parametros-reservas.md).

## Códigos de error estables

Los errores 4xx de Reservas que el frontend necesita distinguir sin leer el texto de `detail` (que puede cambiar
de redacción, de idioma o según un parámetro configurado) llevan además un código estable en la extensión
`codigo` del `ProblemDetails` de respuesta: `{ "status": 409, "detail": "...", "codigo": "LIMITE_VIGENTES" }`. El
mensaje en español se conserva igual, para mostrarlo al usuario; el código es solo para que el cliente decida qué
hacer. Un error sin código asignado sigue funcionando igual que antes (el frontend cae a su tratamiento genérico).

| Código | Cuándo ocurre | HTTP |
|---|---|---|
| `LIMITE_VIGENTES` | El teléfono ya alcanzó el máximo de reservas vigentes configurado, al crear. | 409 |
| `CANCELACION_FUERA_DE_PLAZO` | Se intentó cancelar por código fuera del límite de cancelación configurado. | 409 |
| `CAMBIO_CONCURRENTE` | Otra solicitud modificó la misma reserva antes de que esta terminara de procesarse (check-in, modificar, cancelar, completar, marcar no asistida). | 409 |
| `CONTACTAR_RESTAURANTE` | El grupo supera el tamaño máximo configurado (al consultar disponibilidad o al crear): el canal web no resuelve este caso. | 400 |
| `RESERVA_NO_CANCELABLE` | La reserva ya no está Confirmada (Cancelada, NoAsistio, EnCurso o Completada): no se puede cancelar por código. | 409 |
| `ZONA_SIN_MESAS` | La zona elegida (solo canal web) ya no tiene mesas disponibles en esa franja, al crear. | 409 |
| `LIMITE_IP_DIARIO` *(reservado, no implementado)* | Esa IP alcanzó el máximo de reservas web del día de negocio actual. | 409 |
| `TELEFONO_BLOQUEADO_NOSHOW` *(reservado, no implementado)* | Ese teléfono tiene inasistencias recientes y el canal web no lo acepta. | 409 |

`LIMITE_IP_DIARIO` y `TELEFONO_BLOQUEADO_NOSHOW` están declarados en `CodigosErrorReservas.cs` para fijar el nombre
final, pero el backend **no los devuelve**: quedan reservados para las mejoras futuras (ver más abajo). No se quitan
ni se usan.

## Zona opcional en la reserva web

En `/reservar` el cliente puede elegir **una zona** (por ejemplo Salón o Terraza) o dejar «Sin preferencia».

- **Cuándo aparece el selector.** Después de elegir la franja, y solo si **2 o más zonas tienen una mesa libre de
  capacidad suficiente en esa franja**. Con una sola zona con lugar (o una sola zona activa) no se pregunta nada y la
  mesa se asigna como siempre. Las zonas llenas **no se ofrecen** (se ocultan, no se deshabilitan). La lista sale de
  `zonasDisponibles` en cada franja de `GET /api/reservas/disponibilidad`; la disponibilidad del personal no la lleva.
- **Cómo se asigna.** Con zona elegida, la mesa se busca solo entre las mesas de esa zona (capacidad suficiente, la de
  menor capacidad primero, sin reservas ni visitas en esa fecha y franja). Sin zona, el comportamiento es el de siempre
  (menor capacidad de todo el salón). `POST /api/reservas` acepta `zonaId` opcional; una zona inexistente o inactiva se
  rechaza con 400. **El canal del personal ignora `zonaId`.**
- **Zona llena al confirmar.** Si la zona elegida se llenó mientras el cliente completaba sus datos, la respuesta es
  409 con código `ZONA_SIN_MESAS` («La zona X ya no tiene mesas disponibles en esa franja. Elige otra zona o "sin
  preferencia".»). Nunca se asigna en silencio una mesa de otra zona. El frontend decide por el código: vuelve al
  Paso 1 con el mensaje, con la disponibilidad actualizada y la zona sin elegir.
- **Qué ve el cliente al confirmar y al consultar.** `ReservaCreadaDto` y `ReservaConsultadaDto` incluyen `zonaNombre`.
  **Nunca se muestra el número de mesa ni su capacidad**, porque el personal puede mover la reserva de mesa el mismo día.
- **La zona no se guarda como preferencia aparte.** La zona de una reserva es **siempre la de su mesa actual**: si el
  personal la mueve a una mesa de otra zona, la consulta posterior por código muestra la zona nueva.
- **Carrera por la última mesa.** Si dos solicitudes simultáneas se disputan la última mesa de una zona, una se
  confirma y la otra recibe **409 sin código** con el texto genérico «Esa mesa ya fue tomada, intenta otra franja.»
  (la zona podría tener todavía otras mesas libres, así que no se usa `ZONA_SIN_MESAS` en ese caso).

## Aviso de próxima reserva al registrar una visita

Al registrar una visita sin reserva en una mesa, el sistema avisa —sin bloquear— si esa mesa tiene una reserva vigente
(Confirmada o EnCurso) en una **franja posterior del mismo día que todavía no ha terminado**.

- **Endpoint:** `GET /api/visitas/advertencia-mesa?mesaId=…&franjaId=…&fecha=…` (la fecha es opcional; por defecto hoy en
  hora de Santo Domingo). Devuelve `hayAviso`, `mensaje`, `franjaNombre`, `horaInicio`, `personas` y `minutosFaltantes`
  (solo cuando la fecha es hoy). **Sin nombre ni teléfono del cliente.**
- **Mensaje:** «La mesa N tiene una reserva a las HH:mm (franja X, P personas), en M minutos. Si registras la visita, la
  mesa podría no estar libre a tiempo.»
- **«Registrar de todos modos».** El aviso no cambia el registro: la visita se registra igual si el personal lo decide.
- **Mapa de ocupación.** La mesa con una reserva posterior ese día muestra **«Próx. HH:mm»** (la más cercana que aún no
  ha terminado; solo en esa mesa). `MesaOcupacionDto` trae `proximaReservaHoraInicio` y `proximaReservaPersonas`. La
  visita sin reserva no cuenta como «próxima»: no tiene hora de salida.

## Texto de cancelación

El texto que repiten Confirmación y «Mi reserva · Detalle» **ya no usa la palabra «gratis»**: cancelar no tiene costo en
ningún caso, así que solo se indica el plazo («Puedes cancelar hasta 2 horas antes de tu franja. Después, llama al … /
contacta directamente al restaurante.»; con límite 0: «Puedes cancelar hasta el inicio de la franja…»). Sale del
parámetro de límite de cancelación y del teléfono de contacto configurados.

## Limitaciones conocidas

- **El teléfono solo se valida por formato** (809/829/849 + 7 dígitos): no se verifica que exista ni que sea de quien
  reserva. Las únicas barreras contra reservas falsas son el máximo de reservas vigentes por teléfono y la limitación de
  tasa (en creación: 30/min por usuario autenticado y 5/min por IP para invitados).
- **Los estados de la API son números** (enums sin convertir a texto): decisión del equipo; el frontend los traduce.
- **Códigos reservados sin implementar** (`LIMITE_IP_DIARIO`, `TELEFONO_BLOQUEADO_NOSHOW`): ver «Mejoras futuras».
- **Carrera por la última mesa:** el segundo solicitante recibe un 409 sin código (ver «Zona opcional»).

## Mejoras futuras (no implementadas)

Decididas como fuera del alcance actual; no hay código, parámetros ni migraciones para ellas.

- **IP real tras un proxy** (`ForwardedHeaders` con `KnownProxies`/`KnownNetworks` de configuración): hoy los limitadores
  leen `RemoteIpAddress`, que tras un proxy es la IP del proxy.
- **Límite diario de reservas web por IP** (con hash de la IP, nunca en claro) y **bloqueo web por reputación de
  no-show** (por teléfono y ventana de días); sus códigos `LIMITE_IP_DIARIO` y `TELEFONO_BLOQUEADO_NOSHOW` quedan reservados.
- **CAPTCHA**, **tope de mesas por la web** y **verificación del teléfono por SMS**.
- **Estados como texto en la API** (en vez de números).
- **«Terminar visita»**: dar a la visita sin reserva una hora de salida para liberar la mesa antes de que acabe la franja.
