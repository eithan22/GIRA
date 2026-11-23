# Analítica e IA y Dashboard Gerencial

**Estado de referencia:** 21 de noviembre de 2026.

Este documento describe la implementación realizada para la épica GI-8, sus
dependencias y los pendientes para su puesta en operación. El estado de una
historia en Jira no sustituye la validación técnica ni funcional.

## 1. Objetivo y alcance

El módulo permite consultar la afluencia real, pronosticar demanda por fecha y
franja, revisar indicadores operativos y apoyar decisiones mediante
recomendaciones. Consume información de Reservas y Visitas, Inventario,
Personal y Configuración; no reemplaza la responsabilidad de esos módulos.
La implementación actual corresponde a un único restaurante.

La referencia funcional es la épica GI-8 y sus historias. Durante el trabajo
se consultó el SRS; sus archivos y referencias presentan diferencias de versión,
por lo que el equipo debe confirmar cuál es la versión oficial para la entrega.

## 2. Implementación realizada

### 2.1 Estructura e integración

- Se organizaron entidades de Analítica en Domain; contratos, DTO, manejadores
  y casos de uso en Application; repositorios, SQL Server, ML.NET y procesos de
  fondo en Infrastructure; y controladores en API.
- Se registraron las dependencias y políticas de autorización existentes. El
  frontend consume los endpoints mediante el servicio de Analítica.
- Se añadieron migraciones de historial, integridad, predicciones, generaciones,
  recomendaciones, entrenamiento y fuentes de indicadores.

Ubicaciones principales: [entidades](../GIRA.Domain/Entities/Analitica),
[servicios](../GIRA.Application/Services/Analitica),
[repositorios](../GIRA.Infrastructure/Persistence/Repositories/Analitica),
[controladores](../GIRA.Api/Controllers/Analitica) y
[dashboard](../Gira.frontend/src/analitica).

### 2.2 Historial de afluencia

- `VisitaCompletadaEvent` registra personas realmente atendidas, tanto de
  reservas como de visitas sin reserva. Una reserva confirmada no acredita
  asistencia.
- `ReservaNoAsistidaEvent` registra el no-show por separado, sin sumarlo a
  los comensales atendidos.
- El historial se agrupa por fecha y franja. Un índice por origen y referencia
  evita duplicados; un evento repetido con contenido contradictorio se rechaza.
- Los datos inválidos y errores de persistencia se registran sin propagarse al
  productor ni impedir que se ejecuten los siguientes handlers. La operación
  de origen puede completarse aunque el historial no se haya guardado; requiere
  revisión y conciliación. MediatR en memoria **no garantiza entrega durable
  ni recuperación automática después de una caída**.
- La ausencia de eventos no se interpreta automáticamente como un día con
  cero asistentes. El historial anterior a la captura no se reconstruye.

Código: [manejador de eventos](../GIRA.Application/EventHandlers/Analitica/AfluenciaPorReservasHandler.cs)
y [repositorio de afluencia](../GIRA.Infrastructure/Persistence/Repositories/Analitica/RegistroAfluenciaRepository.cs).

### 2.3 Entrenamiento y predicciones

- Se implementó SSA con ML.NET por franja, con horizonte de hasta 14 días e
  intervalos de confianza del 95 %.
- El entrenamiento requiere 70 fechas observadas consecutivas por franja:
  56 para entrenar y 14 para validar. Se calculan MAE y RMSE; el candidato se
  activa cuando su MAE es igual o mejor que la referencia vigente.
- Los trabajos se solicitan desde el dashboard o API, se procesan en segundo
  plano y conservan estado, resultados y errores. El procesador está habilitado
  en la configuración base, pero **el entrenamiento se solicita bajo demanda**;
  no existe un programador diario de entrenamiento.
- Los modelos, sus versiones y métricas se guardan en SQL Server. La
  reactivación manual de una versión anterior exige motivo y queda auditada.
- La generación diaria de predicciones está desactivada en la configuración
  base. Una vez validada con Inventario se puede habilitar; entonces se comprueba
  cada cinco minutos después del cierre configurado, mientras la API permanece
  activa. Usa SSA si hay un modelo activo y observaciones posteriores continuas.
- Si no puede usar SSA, utiliza el promedio de fechas observadas del mismo día
  de la semana y franja dentro de los últimos 28 días, con baja confianza.
  Cuando no hay observaciones comparables no inventa un pronóstico. Si no
  obtiene ningún resultado, no marca la generación diaria como completada y
  vuelve a intentar en el siguiente ciclo.
- Se conservan predicciones anteriores y se identifica la vigente. La
  publicación hacia Inventario se guarda para reintento y solo se prepara
  para fechas con predicción de todas las franjas activas.

#### Contrato para Inventario y Personal

`PrediccionAfluenciaGeneradaEvent` mantiene `Desde`, `Hasta` y `Predicciones`.
Analítica agrega al final los siguientes campos:

- `EventoId`: GUID persistido de la publicación de un día; permanece igual
  al reintentar esa publicación. Es el identificador para deduplicar eventos.
- `VersionContrato`: `1`; identifica la estructura del evento, no el modelo.
- `RestauranteId`: GUID real configurado en `Analitica:RestauranteId`.
- `OcurridoEnUtc`: fecha y hora UTC de la generación persistida.
- `TipoEvento`: `PrediccionAfluenciaGeneradaEvent`.

Cada elemento de `Predicciones` añade `PrediccionId` y `VersionModelo`: son el
ID persistido y la versión real de esa fecha y franja. Un mismo evento puede
contener versiones de modelo diferentes. No confundir el ID del evento con los
IDs de sus predicciones.

Los campos nuevos son opcionales en los constructores para mantener el código
existente compatible; los eventos antiguos o construidos sin ellos contienen
`null`. Los consumidores que los necesiten deben validar su presencia, versión
y restaurante antes de actuar. Los eventos producidos por Analítica incluyen
los metadatos; falta coordinar y probar el consumidor de Personal con Jean.
El restaurante es único y se configura externamente, no se inventa ni se valida
contra un catálogo de restaurantes. El equipo debe mantener el mismo ID para
las publicaciones pendientes, incluidos los reintentos.

Código: [entrenamiento](../GIRA.Application/Services/Analitica/EntrenamientoModeloService.cs),
[generación](../GIRA.Application/Services/Analitica/GeneracionPrediccionService.cs)
y [modelo SSA](../GIRA.Infrastructure/Services/Analitica/ModeloSsaService.cs).

### 2.4 Indicadores y fuentes

Los indicadores usan el período consultado, salvo las alertas abiertas, que
reflejan el estado actual. Si faltan fuentes fiables o un denominador es cero,
el resultado puede ser `null`; el dashboard muestra ese estado en lugar de
inventar un valor.

| Indicador | Fuente y cálculo implementado |
|---|---|
| Comensales atendidos | Personas de visitas completadas con y sin reserva. |
| Ocupación | Comensales del período / capacidad acumulada × 100. Requiere una captura de capacidad para cada fecha del período. |
| No-show | Reservas no asistidas / (reservas completadas + no asistidas) × 100. |
| Cancelación | Reservas creadas en el período que están canceladas al consultar / reservas creadas en el período × 100. |
| Sin reserva | Comensales de visitas sin reserva / comensales atendidos × 100. |
| Precisión del modelo | MAE: promedio de la diferencia absoluta entre predicción y asistencia real por fecha y franja; se usa el pronóstico más reciente generado antes del inicio local del día. Un MAE menor indica menor error. |
| Costo por comensal | Costo de salidas y mermas / comensales atendidos. |
| Merma | Costo de mermas / costo total de salidas y mermas × 100. |
| Alertas abiertas | Conteo actual de alertas abiertas de Inventario. |
| Horas por comensal | Horas de turnos cerrados, descontadas las pausas / comensales atendidos. |

También se presenta el promedio de asistencia por día de semana y franja,
usando fechas observadas. Las franjas se muestran con sus nombres actuales.

La capacidad diaria se captura antes de la apertura usando la suma de
capacidades de mesas activas en zonas activas, multiplicada por el número de
franjas activas. Un día cerrado se captura con capacidad cero. No se rellenan
fechas pasadas con la configuración actual; si la API no está disponible antes
de abrir, puede faltar la captura de ese día.

Inventario registra costos en los movimientos. Para insumos perecederos se
valora el consumo por lote FEFO y costo promedio ponderado; una entrada sin
costo explícito usa el costo de referencia del insumo. Movimientos o lotes
antiguos sin costo no se reconstruyen y pueden dejar el indicador no disponible.
Las horas usan turnos cerrados cuyo inicio pertenece al período, no una
distribución de un turno entre varias fechas.

Código: [cálculo de KPI](../GIRA.Application/Services/Analitica/KpisService.cs),
[fuentes SQL](../GIRA.Infrastructure/Persistence/Repositories/Analitica/KpisRepository.cs),
[capacidad](../GIRA.Application/Services/Analitica/CapacidadOperativaService.cs)
y [movimientos de inventario](../GIRA.Application/Services/Inventario/MovimientoInventarioService.cs).

### 2.5 Recomendaciones

Se generan desde la información disponible y se actualizan al iniciar la API y
cada cinco minutos. Las reglas configuradas son:

- Demanda prevista al menos 30 % superior al promedio observado del mismo día y
  franja durante los últimos 28 días.
- No-show de los últimos siete días superior al 15 %.
- Más de dos alertas del mismo insumo en el mes calendario.
- Aviso cuando una predicción tiene baja confianza.

Los umbrales son configurables. Las recomendaciones se guardan, se consultan y
pueden marcarse como vistas o descartadas. Al generar una nueva se publica
`RecomendacionGeneradaEvent`; un fallo de entrenamiento publica
`EntrenamientoModeloFallidoEvent`. Esos eventos son puntos de integración,
**no notificaciones entregadas**.

Código: [reglas](../GIRA.Application/Services/Analitica/ReglasRecomendaciones.cs)
y [servicio](../GIRA.Application/Services/Analitica/RecomendacionesService.cs).

### 2.6 Dashboard y experiencia de uso

- Dashboard protegido para Gerente y Administrador, disponible en
  `/analitica/dashboard` y `/gerencia/dashboard`.
- Tarjetas de indicadores, promedio por día y franja, gráfica de asistencia,
  alertas actuales, recomendaciones, modelos y calendario de feriados.
- El período principal actualiza los indicadores y el historial; la fecha de
  predicción se inicializa con el final del período aplicado. Después se puede
  consultar otra fecha y filtrar por franja.
- La comparación de predicción y asistencia se relaciona por fecha y franja;
  indica versión, confianza, límites y fecha de generación cuando existen.
- Se diferencian carga, error, ausencia de datos y baja confianza; se ofrece
  reintento. Los mensajes de entrenamiento distinguen un trabajo finalizado de
  un modelo que no pudo entrenarse por datos insuficientes.
- Se ajustaron los estilos para escritorio y móvil y la navegación por teclado.
  La gráfica principal de afluencia muestra los últimos 14 días del período.
- Los feriados reutilizan la configuración de días cerrados existente.

Código: [dashboard](../Gira.frontend/src/analitica/Dashboard.jsx),
[predicciones](../Gira.frontend/src/analitica/Predicciones.jsx),
[modelos](../Gira.frontend/src/analitica/ModelosAnalitica.jsx) y
[calendario](../Gira.frontend/src/analitica/CalendarioFeriados.jsx).

### 2.7 Avance para Personal y Notificaciones

Se dejó una base de marcaciones para el usuario autenticado: consultar turno
abierto, marcar entrada y cerrar con minutos de pausa. La pantalla «Mi turno»
usa esos servicios y Analítica consume los turnos cerrados. No incluye
planificación de horarios, nómina ni una gestión completa de personal.

Notificaciones dispone de los eventos mencionados en la sección 2.5, pero
falta que su responsable implemente el consumidor y acuerde destinatarios,
persistencia, entrega, reintentos y monitoreo. No se añadió un módulo genérico
de modificaciones; el cambio de modelo ya tiene auditoría propia.

Código: [marcaciones API](../GIRA.Api/Controllers/Personal/MarcacionesController.cs),
[pantalla de marcaciones](../Gira.frontend/src/personal/Marcaciones.jsx) y
[eventos de Analítica](../GIRA.Application/Events/Analitica).

## 3. API y permisos

Las fechas de consulta son locales e inclusivas. Los ejemplos completos están
en [GIRA.Api.http](../GIRA.Api/GIRA.Api.http). Las operaciones requieren sesión
y política correspondiente.

| Operación | Ruta | Acceso |
|---|---|---|
| Consultar historial | `GET /api/analitica/historial?desde=&hasta=` | Gerente, Administrador |
| Consultar indicadores | `GET /api/analitica/kpis?desde=&hasta=` | Gerente, Administrador |
| Consultar predicciones | `GET /api/analitica/prediccion?desde=&hasta=&franjaId=` | Supervisor, Gerente, Administrador |
| Solicitar entrenamiento | `POST /api/analitica/modelos/entrenamientos` | Gerente, Administrador |
| Consultar trabajo | `GET /api/analitica/modelos/entrenamientos/{id}` | Gerente, Administrador |
| Listar modelos | `GET /api/analitica/modelos` | Gerente, Administrador |
| Reactivar modelo anterior | `POST /api/analitica/modelos/{modeloId}/activar` | Gerente, Administrador |
| Consultar recomendaciones | `GET /api/analitica/recomendaciones?estado=` | Gerente, Administrador |
| Cambiar recomendación | `PATCH /api/analitica/recomendaciones/{id}` | Gerente, Administrador |
| Consultar, crear o eliminar días cerrados | `GET`, `POST /api/feriados`; `DELETE /api/feriados/{id}` | Gerente, Administrador |
| Consultar turno, registrar entrada y salida | `GET /api/personal/marcaciones/actual`; `POST /api/personal/marcaciones/entrada` y `/salida` | Usuario autenticado con política de turnos propios |

La consulta de predicciones admite `franjaId` opcional y un rango de hasta
366 días. El entrenamiento devuelve `202 Accepted` y un identificador; se
consulta su resultado por separado. La activación requiere un motivo.

## 4. Configuración y operación

1. Preparar .NET, Node.js y SQL Server conforme al
   [README](../README.md), con conexión y credenciales fuera del repositorio.
2. Aplicar las migraciones de forma controlada. En Development, la API aplica
   migraciones al arrancar; no asumir ese comportamiento para producción.
3. Configurar horarios, cierres, zonas, mesas y franjas reales. Verificar los
   costos de entrada y que las visitas y marcaciones se registren al operar.
4. Iniciar la API y el frontend. La API debe permanecer activa para capturar
   capacidad, procesar entrenamientos, generar predicciones y recomendaciones.
5. Solicitar entrenamiento cuando exista historial suficiente y consultar el
   resultado por franja. Tras validar la generación con Inventario, habilitarla
   y revisar su ejecución después del cierre y los logs si no aparecen resultados.

En [appsettings.json](../GIRA.Api/appsettings.json) la generación está
desactivada (`Analitica:Generacion:Habilitada=false`). Capacidad, entrenamiento
y recomendaciones están habilitados. Las variables o archivos del entorno
pueden sobrescribir esos valores. Estar habilitado no garantiza que existan
observaciones, un modelo entrenado ni una ejecución exitosa.

| Proceso habilitado | Frecuencia | Efecto |
|---|---|---|
| Capacidad | Al iniciar y cada minuto | Captura capacidad diaria antes de la apertura cuando corresponde. |
| Entrenamiento | Procesa solicitudes pendientes; espera 5 segundos cuando no hay trabajo | Guarda estados y resultados de entrenamientos solicitados. |
| Recomendaciones | Al iniciar y cada 5 minutos | Guarda o actualiza recomendaciones y publica eventos de las nuevas. |

La generación, cuando se habilita, revisa cada cinco minutos la generación
posterior al cierre y las publicaciones pendientes hacia Inventario. Antes de
habilitarla, coordinar con Inventario y Personal y configurar
`Analitica:RestauranteId` (o `Analitica__RestauranteId`) con un GUID real no
vacío. Si falta o es inválido, la API no inicia con generación habilitada.

## 5. Validaciones realizadas

- La ejecución de Analítica del 21 de noviembre incluyó **80 pruebas .NET**,
  entre ellas pruebas SQL específicas; todas pasaron.
- Se ejecutaron **9 pruebas de navegador** del dashboard con respuestas
  controladas; pasaron. Cubren filtros, comparación, estados, permisos,
  entrenamiento, feriados, teclado y adaptación.
- Una ejecución de integración aislada comprobó el flujo de entrenamiento SSA,
  predicción, recomendaciones, KPI y representación del dashboard con SQL
  Server y navegador. No equivale a una validación de operación real continua.
- En una validación anterior pasaron 207 pruebas unitarias frontend, lint y
  build. El build informó una advertencia de bundle JavaScript mayor a 500 kB.

Los comandos y requisitos de pruebas se encuentran en el
[README](../README.md). La prueba de navegador con respuestas controladas no
demuestra por sí sola el funcionamiento de la API o de SQL Server.

## 6. Pendientes para completar y entregar

| Prioridad | Pendiente | Responsable o dependencia |
|---|---|---|
| Alta | Confirmar el SRS oficial y conciliar sus criterios con GI-8 y las ocho historias. Aclarar las referencias a varios restaurantes frente al alcance actual de un único restaurante. | Equipo funcional y arquitectura |
| Alta | Validar con datos operativos reales el registro de visitas y no-shows, cancelaciones, costos, capturas de capacidad y marcaciones; comprobar los indicadores con sus responsables. | Reservas, Inventario, Personal y gerencia |
| Alta | Validar con Inventario antes de habilitar la generación. Mantener la API activa antes de la apertura y después del cierre para comprobar la captura y la generación en condiciones reales; monitorear errores y definir alertas operativas. | Inventario, infraestructura y Analítica |
| Alta | Revisar con el responsable de Inventario los cambios de costo en movimientos y lotes, y las migraciones relacionadas, antes de integrar a DEV. | Inventario |
| Alta | Integrar Notificaciones con los eventos de recomendaciones y fallos de entrenamiento; definir entrega y reintentos. | Responsable de Notificaciones, GI-93 |
| Alta | Diseñar y acordar entrega durable o conciliación para eventos de afluencia perdidos; MediatR en memoria no recupera eventos tras una caída. | Reservas, Analítica e infraestructura |
| Alta | Ampliar e integrar Personal/Turnos según el alcance aprobado; conservar el contrato de horas reales o adaptar la consulta de Analítica de forma coordinada. | Responsable de Personal, GI-92 |
| Alta | Revisar credenciales de desarrollo expuestas durante una demostración anterior y rotarlas si siguen vigentes; no incluirlas en documentación ni código. | Responsable del entorno |
| Media | Verificar concurrencia, publicación de predicciones y reconciliación de recomendaciones antes de ejecutar varias instancias de la API. | Analítica e infraestructura |
| Media | Acordar si se requiere programar entrenamientos recurrentes: hoy se solicitan bajo demanda. Definir monitoreo y procedimiento de reintento autorizado. | Gerencia y Analítica |
| Media | Definir cómo señalar en el dashboard recomendaciones desactualizadas cuando falla su actualización. | Analítica y frontend |
| Media | Verificar el rendimiento y la precisión con volumen, infraestructura y datos reales; acordar métricas operativas y umbrales de alerta o rollback si se requieren. | QA e infraestructura |
| Media | Resolver o conciliar los criterios pendientes de las historias y subtareas de Jira; registrar la evidencia antes de cerrarlas. | Responsable de Jira y QA |

Las pruebas de navegador del dashboard usan Playwright como dependencia de
desarrollo; el equipo debe tenerla en cuenta al preparar el entorno de pruebas.

Los datos históricos previos sin eventos, snapshots, costos o marcaciones
fiables no se recuperan automáticamente. Incorporarlos requiere acordar una
fuente verificable y un procedimiento de conciliación o migración; hasta
entonces algunos indicadores y modelos pueden permanecer no disponibles.

## 7. Estado de Jira consultado

Tras la actualización del 21 de noviembre de 2026, GI-8 y las historias GI-9,
GI-18, GI-28, GI-39, GI-50, GI-60, GI-71 y GI-80 figuran **Por hacer**.
Sus 75 subtareas suman **53 Listo** y **22 Por hacer**.

Las historias permanecen abiertas porque tienen criterios o verificaciones
pendientes, aunque contengan trabajo implementado. En Jira se explican las
reaperturas y los criterios que requieren conciliación, como el alcance de un
solo restaurante y la programación del entrenamiento. El estado refleja la
evidencia revisada del árbol local, no un despliegue en producción.
