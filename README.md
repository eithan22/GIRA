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
