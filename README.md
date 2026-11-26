# GIRA — Gestión Inteligente de Restaurantes Automatizada

Proyecto final de ITLA (Idea N.º 26). Backend en ASP.NET Core (.NET 10) con Clean Architecture. El frontend React vive en la carpeta Gira.frontend/ de este repositorio.

## Arquitectura en capas

```
GIRA.Domain  <-  GIRA.Application  <-  GIRA.Infrastructure  <-  GIRA.Api
```

**Regla de oro:** una capa solo puede depender de las que están a su izquierda. `GIRA.Domain` no depende de nada. `GIRA.Api` depende de `GIRA.Application` y `GIRA.Infrastructure`, nunca al revés.

| Proyecto | Contenido |
|---|---|
| `GIRA.Domain` | Entidades, enums, constantes; sin dependencias externas |
| `GIRA.Application` | Casos de uso, DTOs, validadores de FluentValidation, interfaces |
| `GIRA.Infrastructure` | `GiraDbContext`, configuraciones EF, migraciones, repositorios, servicios |
| `GIRA.Api` | Controllers, `Program.cs`, middlewares, configuración |

Dentro de cada capa, el código se organiza **por módulo** (una carpeta por módulo: Seguridad, Reservas, Inventario, Personal, Analítica, Configuración).

## Requisitos

- .NET 10 SDK
- Docker Desktop (solo para SQL Server; la API y el frontend corren normal en la máquina)
- En Ubuntu, `sudo apt-get update && sudo apt-get install -y libomp-dev` para la dependencia nativa `libiomp5.so` de ML.NET SSA, necesaria tanto para entrenar modelos como para ejecutar sus pruebas.

## Levantar SQL Server

```bash
cp .env.example .env   # define tu propia contraseña de SA en .env
docker compose up -d
```

EF Core crea la base (`GIRA_DB`), los esquemas y las tablas al aplicar migraciones; no se crea nada a mano.

## User Secrets necesarios

Configúralos en `GIRA.Api` (solo nombres de claves, nunca valores reales en este documento):

- `ConnectionStrings:GiraDb`
- `Seed:AdminEmail`
- `Seed:AdminPassword`
- `Jwt:Key` (mínimo 32 caracteres)

```bash
dotnet user-secrets set "Jwt:Key" "TU_CLAVE" --project GIRA.Api
```

En producción, los orígenes de CORS se configuran con variables de entorno, no con User Secrets: `Cors__OrigenesPermitidos__0`, `Cors__OrigenesPermitidos__1`, etc.

## Correr la API

```bash
dotnet run --project GIRA.Api
```

En Development, la API aplica migraciones y ejecuta el seeder (roles y administrador inicial) automáticamente al arrancar. Verifica con `/swagger`.

### Generación de predicciones de Analítica

Para el informe de avances, pantallas e integraciones, consulta el [README de Analítica e IA](docs/README-analitica-ia.md). Para fuentes, permisos y pendientes técnicos, consulta la [documentación de Analítica e IA](docs/analitica-ia-dashboard.md).

La generación automática está desactivada por defecto. Tras validarla con Inventario y Personal, configurar `Analitica:RestauranteId` con el GUID real acordado y habilitar `Analitica:Generacion:Habilitada=true` (variables de entorno `Analitica__RestauranteId` y `Analitica__Generacion__Habilitada`). Si se habilita sin un GUID válido y no vacío, la API falla al iniciar. Cuando está habilitada, se revisa cada cinco minutos después del cierre configurado. Usa el modelo SSA activo cuando dispone de observaciones continuas; si no, utiliza un promedio semanal de baja confianza para las fechas con observaciones comparables. Sin datos suficientes no inventa pronósticos ni marca la generación diaria como completada. Inventario solo recibe días con predicciones para todas las franjas activas. El proceso requiere que la API permanezca en ejecución; la publicación se guarda para reintentar fallos, pero todavía no está validada para varias instancias concurrentes.

### Recomendaciones de Analítica

Se calculan al iniciar la API y cada cinco minutos a partir de los datos disponibles. La comparación de demanda usa las fechas observadas de los últimos 28 días, para el mismo día de la semana y franja; la no asistencia usa los últimos siete días y las alertas recurrentes el mes calendario actual. La baja confianza genera un aviso de tipo Analítica.

Los umbrales de `Analitica:Recomendaciones` se configuran en el servidor: `PorcentajeAumentoDemanda` (30 % o más), `PorcentajeNoShow` (más de 15 %) y `AlertasMensuales` (más de 2). El proceso se desactiva con `Habilitada=false`. Los fallos del cálculo se registran en los logs y se reintentan en el siguiente ciclo; el dashboard todavía no indica si los datos quedaron desactualizados. La recepción por el módulo de Notificaciones está pendiente y la reconciliación entre varias instancias no se ha validado.

Con la política `VerDashboard`, `GET /api/analitica/recomendaciones` consulta las vigentes y admite el filtro `estado=Nueva|Vista|Descartada`; `PATCH /api/analitica/recomendaciones/{id}` cambia su estado a `Vista` o `Descartada`.

### Fuentes de los indicadores y disponibilidad histórica

`GET /api/analitica/kpis?desde=AAAA-MM-DD&hasta=AAAA-MM-DD` calcula MAE solo con visitas completadas que tengan un pronóstico generado antes del inicio local de cada día. La ocupación usa snapshots diarios capturados antes de abrir; `Analitica:Capacidad:Habilitada` controla el proceso. Los días anteriores a la captura no se rellenan con la configuración actual, por lo que un período incompleto devuelve el indicador como no disponible.

El costo de salidas y mermas usa el costo de referencia del insumo si no se proporciona un costo de entrada. En insumos perecederos, el consumo se valora por lote FEFO y costo unitario promedio ponderado; si falta costo en algún lote consumido, ese movimiento queda sin costo y los KPI de costos no se estiman parcialmente. Los movimientos y lotes históricos sin costo permanecen sin costo después de la migración.

El KPI de horas por comensal requiere turnos cerrados. Cada usuario registra su turno en `POST /api/personal/marcaciones/entrada` y lo cierra en `POST /api/personal/marcaciones/salida`, indicando la pausa en minutos; la pantalla «Mi turno» usa esos mismos endpoints. Los datos se asocian al usuario autenticado y no se reconstruyen retrospectivamente. Por estas limitaciones, un KPI `null` significa que la fuente del período no es suficiente para una cifra confiable, no que el valor sea cero.

### Avance para Personal/Turnos y Notificaciones

Analítica ya consume las marcaciones cerradas disponibles para calcular horas por comensal, sin administrar horarios ni nómina. La persona responsable de GI-92 puede ampliar esa fuente con turnos planificados o estados laborales; cualquier cambio del contrato debe actualizar la consulta de Analítica, no añadir esos datos al dashboard mediante valores calculados o estimados.

Analítica publica `EntrenamientoModeloFallidoEvent` y, al persistir una recomendación nueva, `RecomendacionGeneradaEvent`. La integración con GI-93 queda pendiente: su responsable debe registrar los handlers y decidir persistencia, entrega y reintentos. MediatR en memoria no garantiza entrega durable ni reprocesamiento, por lo que estos eventos no deben presentarse como notificaciones entregadas.

No se requiere un módulo genérico de modificaciones para Analítica. La activación y el rollback manual de modelos ya se auditan mediante `AuditoriaActivacionModelo`, con usuario, motivo y modelos implicados.

## Correr las pruebas

**Pruebas normales** (no requieren Docker ni secretos):

```bash
dotnet test GIRA.Tests --filter "Category!=Integration"
```

**Pruebas de integración** (requieren Docker arriba y un User Secret adicional en `GIRA.Tests`, apuntando a una base de datos **separada** de `GIRA_DB`):

```bash
dotnet user-secrets set "ConnectionStrings:GiraGi96Test" "Server=localhost,1433;Database=GIRA_GI96_TEST;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True" --project GIRA.Tests

dotnet test GIRA.Tests --filter "FullyQualifiedName~Integration"
```

La base de pruebas debe llamarse exactamente `GIRA_GI96_TEST`. Nunca usar `GIRA_DB` para pruebas.

La prueba de rendimiento del dashboard es independiente: utiliza `ConnectionStrings:GiraDb` de los secretos de la API solo para conectarse al servidor, crea una base temporal `GIRA_ANALITICA_BENCH_<GUID>` y la elimina al finalizar. Requiere permiso para crear y eliminar esa base, Node.js, dependencias frontend instaladas y Chromium de Playwright o `PLAYWRIGHT_CHROME_EXECUTABLE_PATH` apuntando a Chrome. Carga 14.600 eventos sintéticos en 365 días y mide cinco actualizaciones de una sesión del navegador, con la API y SQL Server reales; los datos de predicciones y alertas están vacíos. Ejecútala explícitamente con:

```bash
dotnet test GIRA.Tests --filter "FullyQualifiedName~DashboardRendimientoSqlTests" --logger "console;verbosity=detailed"
```

La integración frontend aislada, con respuestas simuladas y sin modificar bases de datos, se ejecuta en `Gira.frontend` con `npm run test:dashboard`.

## Convenciones por módulo

**Acordado por el equipo:**
- Repositorio por agregado: interfaz en `Application/Interfaces/Repository/<Modulo>`, implementación en `Infrastructure/Repositories/<Modulo>`.
- Servicios por módulo: interfaz en `Application/Interfaces/Services/<Modulo>`.
- Registro de DI con un método `Add<Modulo>()` en `Services/<Modulo>/<Modulo>ServiceExtensions.cs`.
- Namespace = carpeta, siempre.

**Recomendado (no obligatorio):** cuando un módulo tenga varias subáreas claramente separables, organizar los servicios por subárea dentro de esas mismas carpetas: interfaz en `Application/Interfaces/Services/<Modulo>/<Subarea>`, implementación en `Application/Services/<Modulo>/<Subarea>`, con un `Add<Subarea>Service()` por subárea llamado desde `Add<Modulo>()`. Es la plantilla que dejó Seguridad, no una regla acordada por el equipo; un módulo sin subáreas claras no necesita adoptarla. Los repositorios siguen siempre la regla del módulo (`<Modulo>`), nunca se subdividen por subárea.

Si el nombre de una subcarpeta de subárea coincide con una clase estática de constantes que el archivo use (caso real: la subcarpeta `Roles` contra `GIRA.Domain.Constants.Roles`), usar un alias en ese archivo — `using RolesConst = GIRA.Domain.Constants.Roles;` — y **nunca** renombrar la subcarpeta para evitar el choque.

### Seguridad: la excepción documentada y la plantilla a imitar

Seguridad no sigue la convención de arriba al pie de la letra: sus servicios viven en `GIRA.Infrastructure/Services/Seguridad`, no en `Application/Services/Seguridad`, porque dependen directamente de ASP.NET Core Identity (`UserManager`, `SignInManager`) y de `GiraDbContext`. Los contratos (interfaces, DTOs, validadores) sí viven en `Application`, como en cualquier otro módulo.

Lo que **sí** es la plantilla a imitar — como recomendación, no como regla acordada — es la organización por subárea (Auth, Usuarios, Roles, Accesos), idéntica en todas las capas, aconsejable cuando un módulo tenga varias subáreas:

```
Application/Interfaces/Services/Seguridad/
├── Auth/        IAuthService, ITokenService, IRotadorRefreshToken
├── Usuarios/    IUsuarioService
├── Roles/       IRolService
└── Accesos/     IRegistroAccesoService, IHistorialAccesosService

Application/Validations/Seguridad/
├── Auth/        LoginRequestValidator
├── Usuarios/    CrearUsuarioRequestValidator, ActualizarUsuarioRequestValidator,
│                UsuarioFiltroRequestValidator, AsignarRolesRequestValidator
└── Accesos/     HistorialAccesosRequestValidator

Infrastructure/Services/Seguridad/
├── SeguridadServiceExtensions.cs   (raíz — AddSeguridad)
├── MensajesSeguridad.cs            (raíz — compartido entre subáreas)
├── Auth/        AuthService, RotadorRefreshToken, TokenService,
│                JwtSettings, TiposClaim, MapeadorUsuarioAutenticado
├── Usuarios/    UsuarioService, UsuarioServiceExtensions, GeneradorPasswordTemporal
├── Roles/       RolService, RolServiceExtensions
└── Accesos/     RegistroAccesoService, HistorialAccesosService

Infrastructure/Identity/
└── Usuario, Rol, IdentityErrorDescriberEspanol   (solo lo que Identity exige directamente)

Api/Common/Seguridad/
└── AutenticacionJwtExtensions, LimiteLoginExtensions
```

Decisiones de diseño detrás de ese árbol:

- `UsuarioService` **no se dividió** en varias clases: no hay lógica pura que justifique más clases sin tocar `UsuariosController`/`MeController` y sus tests. Solo se extrajo `GeneradorPasswordTemporal` (generación de la contraseña temporal, sin estado ni dependencias de Identity).
- `GeneradorPasswordTemporal` es `public` (no `internal`) porque `GIRA.Infrastructure` no declara `InternalsVisibleTo` hacia `GIRA.Tests`.
- `ClaimsPrincipalExtensions.ObtenerUsuarioId` se queda en `Api/Common` (no en `Api/Common/Seguridad`): es transversal, lo usarán los controllers de todos los módulos para leer el usuario autenticado.
- `JwtSettings` y `TiposClaim` viven en `Infrastructure/Services/Seguridad/Auth`, no en `Infrastructure/Identity`: son responsabilidad de tokens/claims (Auth), no del modelo de usuario de Identity.

## Cómo agregar un módulo nuevo

1. Crear las carpetas del módulo en cada capa que aplique (`Application/Interfaces/Repository/<Modulo>`, `Application/Interfaces/Services/<Modulo>`, `Infrastructure/Repositories/<Modulo>`, `Infrastructure/Services/<Modulo>`, etc.). Si el módulo tiene varias subáreas claramente separables, se puede organizar `Services` por subárea (`<Modulo>/<Subarea>`), siguiendo el árbol de Seguridad como plantilla; los repositorios siguen siempre la regla del módulo, sin subárea.
2. Implementar el registro de DI del módulo con `Add<Modulo>()` en `Services/<Modulo>/<Modulo>ServiceExtensions.cs`, delegando en un `Add<Subarea>Service()` por subárea cuando el módulo tenga varias, siguiendo el patrón de `AddSeguridad()`.
3. Agregar las políticas de autorización del módulo en `GIRA.Api/Authorization/AutorizacionExtensions.cs` (`AddAutorizacionGira` es compartido; cada módulo agrega ahí las suyas).
4. Escribir las pruebas del módulo (unitarias y, si aplica, de integración).
5. Verificar que el namespace de cada archivo coincida con su carpeta.

## Flujo de ramas y Pull Requests

- Una rama por historia de Jira.
- El PR se abre hacia `DEV` o `Modulo-Seguridad`, según corresponda.
- Solo el líder del equipo fusiona a esas ramas.

## Endpoints de Seguridad

Ver [`docs/proteger-endpoints.md`](docs/proteger-endpoints.md) para la lista completa de endpoints, políticas de autorización y cómo proteger uno nuevo.
