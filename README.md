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

- Repositorio por agregado: interfaz en `Application/Interfaces/Repository/<Modulo>`, implementación en `Infrastructure/Repositories/<Modulo>`.
- Servicios por módulo: interfaz en `Application/Interfaces/Services/<Modulo>`.
- Registro de DI con un método `Add<Modulo>()` en `Services/<Modulo>/<Modulo>ServiceExtensions.cs`.
- **Seguridad es la excepción documentada:** sus servicios viven directamente en `Infrastructure/Services/Seguridad` (no en `Application`) porque dependen de ASP.NET Core Identity y de `GiraDbContext`.
- Namespace = carpeta, siempre.

## Cómo agregar un módulo nuevo

1. Crear las carpetas del módulo en cada capa que aplique (`Application/Interfaces/Repository/<Modulo>`, `Application/Interfaces/Services/<Modulo>`, `Infrastructure/Repositories/<Modulo>`, `Infrastructure/Services/<Modulo>`, etc.).
2. Implementar el registro de DI del módulo con `Add<Modulo>()` en `Services/<Modulo>/<Modulo>ServiceExtensions.cs`, siguiendo el patrón de `AddSeguridad()`.
3. Agregar las políticas de autorización del módulo en `GIRA.Api/Authorization/AutorizacionExtensions.cs` (`AddAutorizacionGira` es compartido; cada módulo agrega ahí las suyas).
4. Escribir las pruebas del módulo (unitarias y, si aplica, de integración).
5. Verificar que el namespace de cada archivo coincida con su carpeta.

## Flujo de ramas y Pull Requests

- Una rama por historia de Jira.
- El PR se abre hacia `DEV` o `Modulo-Seguridad`, según corresponda.
- Solo el líder del equipo fusiona a esas ramas.

## Endpoints de Seguridad

Ver [`docs/proteger-endpoints.md`](docs/proteger-endpoints.md) para la lista completa de endpoints, políticas de autorización y cómo proteger uno nuevo.
