# CLAUDE.md — GIRA

Fuente de verdad del proyecto para cualquier sesión de Claude Code. Léelo antes de tocar código.

## 1. Proyecto
- **Nombre:** GIRA (Gestión Inteligente de Restaurantes Automatizada).
- **Contexto:** proyecto final de ITLA, Idea N.º 26. Equipo de 6 personas.
- **Backend:** ASP.NET Core Web API (.NET), Clean Architecture, SQL Server, EF Core, MediatR, ML.NET.
- **Frontend:** React, dentro de este mismo repositorio pero en la **rama `frontend`** (no en un repo separado). No tocar el frontend desde el trabajo de backend sin coordinar.
- **Documento de requisitos:** SRS GIRA v1.1 (6 módulos, 75 requisitos MoSCoW: 53 Debe, 17 Debería, 5 Podría).

## 2. Estructura de la solución
```
GIRA (solución)
├── GIRA.Api              → Controllers, Program.cs, middlewares, configuración
├── GIRA.Domain           → entidades, enums, eventos de dominio, interfaces; sin dependencias externas
├── GIRA.Application      → casos de uso con MediatR, DTOs, validadores FluentValidation, interfaces
└── GIRA.Infrastructure   → GiraDbContext, configuraciones EF, migraciones, repositorios, servicios externos
```
> Nota: `GIRA.slnx` agrupa estos proyectos en carpetas virtuales `GIRA-API`, `GIRA-CORE/GIRA-Domain`, `GIRA-CORE/GIRA-Application`, `GIRA-Infrastructure`. Esas carpetas **no existen físicamente en disco**; son solo organización visual de la solución en Visual Studio.

**Dependencias permitidas:** API → Infrastructure → Application → Domain. Domain no depende de nada.
Dentro de cada capa, el código se organiza por módulo (carpeta por módulo).

## 3. Módulos y responsables
| Módulo | Responsable(s) |
|---|---|
| 1. Seguridad y Acceso (login/roles) | Todo el equipo (Sprint 0) |
| 2. Reservas | Eithan + Ismael |
| 3. Inventario | David + Luis Antonio Montero |
| 4. Analítica e IA | Isaac |
| 5. Personal y Turnos | Jean |
| 6. Configuración y Notificaciones | Por definir |

Transversales y frontend común: todo el equipo.

## 4. Base de datos (decisión importante)
- **Enfoque:** EF Core Code-First con migraciones versionadas en Git.
- Cada integrante tiene su **propia base de datos local**. No hay base compartida para desarrollo.
- Motor: **SQL Server 2022 en Docker** (`mcr.microsoft.com/mssql/server:2022-latest`). Docker se usa **solo** para la base de datos; la API y el frontend corren normal en la máquina.
- La base de datos **no se crea a mano**: EF Core la crea (`GIRA_DB`, esquemas y tablas) al aplicar migraciones. Solo hace falta tener el contenedor de SQL Server encendido.
- **Archivos en la raíz:** `docker-compose.yml`, `.env.example` (versionado, con contraseña de ejemplo) y `.env` (NUNCA se versiona, ya está en `.gitignore`). El volumen `gira-sqldata` conserva los datos.
- **Connection string** (nombre `GiraDb`):
  ```
  Server=localhost,1433;Database=GIRA_DB;User Id=sa;Password=<la de cada quien>;TrustServerCertificate=True
  ```
  Cada quien la guarda en **User Secrets** (clave `ConnectionStrings:GiraDb`); nunca en `appsettings` versionados.
  Orden de configuración: `appsettings.json` < `appsettings.Development.json` < User Secrets < variables de entorno.
  La app debe lanzar una excepción clara si falta `GiraDb`.
- Un solo `GiraDbContext` (hereda de `IdentityDbContext<Usuario, Rol, Guid>`) y una sola cadena de migraciones, en `GIRA.Infrastructure`. Configuración con Fluent API usando `IEntityTypeConfiguration` y `ApplyConfigurationsFromAssembly`.
- **Un esquema SQL por módulo:** `seguridad`, `reservas`, `inventario`, `personal`, `analitica`, `configuracion`.
- En Development, la API aplica migraciones al arrancar (`db.Database.Migrate()`) y ejecuta el seeder (roles y administrador inicial). Los datos iniciales se versionan en el seeder, nunca en la BD de cada quien.

### Comandos (siempre con proyectos explícitos)
```bash
dotnet ef migrations add <Modulo_Descripcion> --project GIRA.Infrastructure --startup-project GIRA.Api
dotnet ef database update --project GIRA.Infrastructure --startup-project GIRA.Api
dotnet ef migrations remove --project GIRA.Infrastructure --startup-project GIRA.Api
```

### Docker
```bash
docker compose up -d       # encender
docker compose stop        # apagar conservando datos
docker compose down        # quitar contenedor, conserva volumen
docker compose down -v     # BORRA todos los datos; solo para reiniciar de cero
```

### Reglas de migraciones en equipo
1. `git pull` ANTES de crear una migración.
2. Nombres con prefijo de módulo: `Seguridad_...`, `Reservas_...`, `Inventario_...`, `Personal_...`, `Analitica_...`, `Configuracion_...`.
3. NUNCA editar una migración ya fusionada en `main`; crear una nueva.
4. Conflicto en `GiraDbContextModelSnapshot.cs`: `migrations remove`, `git pull`, regenerar la migración.
5. Una migración por cambio lógico; revisar el código generado antes de hacer commit.

Una base en la nube se usará **solo** para la demo final, desplegando desde `main`. Nadie desarrolla contra ella.

## 5. Seguridad
- ASP.NET Core Identity + JWT: access token de 30 min; refresh token de 7 días con rotación, guardado hasheado.
- Roles: Administrador, Gerente, Supervisor, Mesero, Cocina. Autorizar con políticas (constantes en `Politicas`), nunca con nombres de rol sueltos.
- Bloqueo: 5 intentos fallidos, 15 minutos. Contraseña: mínimo 8, mayúscula, minúscula, dígito y símbolo.
- Los clientes NO tienen cuenta: usan código de reserva + teléfono.

## 6. Convenciones de código
- C# con comentarios XML en clases y métodos públicos; nombres de dominio en español (`Reserva`, `Mesa`, `Empleado`), nombres técnicos en inglés cuando sean estándar.
- Todas las entidades heredan de `BaseEntity` (Id Guid, auditoría, soft delete, RowVersion).
- Casos de uso con MediatR (Commands/Queries) + FluentValidation. Errores con `ProblemDetails`.
- Comunicación entre módulos mediante eventos de dominio MediatR, publicados tras `SaveChanges`. Prohibido que un módulo use directamente las entidades internas de otro.
- Logging con Serilog. Notificaciones en tiempo real con SignalR. Rate limiting en login.
- **Reservas:** estados `Confirmada → EnCurso → Completada`, `Cancelada`, `NoAsistio`; transiciones como métodos de la entidad `Reserva`; índice único filtrado en `(MesaId, Fecha, FranjaHorariaId)` contra doble reserva.
- **Analítica:** ML.NET (SSA) entrenado en `BackgroundService`; datos sintéticos para el arranque en frío.

## 7. Flujo de trabajo del equipo
- **Git:** ramas `feature/<modulo>-<descripcion>`, Pull Request hacia `main`, sin push directo a `main`. El frontend vive en la rama `frontend` de este mismo repositorio.
- **Jira:** proyecto GI (https://eithanread1.atlassian.net). Épicas por módulo; el módulo Usuarios y Roles ya tiene historias GI-94 a GI-102 y sus subtareas. Mencionar la clave de Jira en commits y PRs.

## 8. Cómo levantar el entorno (onboarding)
1. Instalar Docker Desktop.
2. Clonar el repositorio.
3. Copiar `.env.example` a `.env` y definir tu propia contraseña (`SA_PASSWORD`).
4. `docker compose up -d`.
5. Configurar User Secrets con `ConnectionStrings:GiraDb`.
6. Ejecutar la API (aplica migraciones y seeder automáticamente).
7. Verificar con Swagger.

## 9. Problemas frecuentes
- **Puerto 1433 ocupado:** cambiar el mapeo a `1434:1433` en `docker-compose.yml` y ajustar la connection string.
- **El contenedor se apaga solo:** la contraseña SA no cumple la complejidad (mínimo 8 caracteres, 3 de 4 tipos: mayúscula, minúscula, dígito, símbolo).
- **Login failed:** la contraseña de User Secrets no coincide con la de `.env`.
- **Mac con Apple Silicon:** usar `platform: linux/amd64` en `docker-compose.yml` y activar Rosetta en Docker Desktop.

## 10. Pendientes
- Responsable de Configuración y Notificaciones.
- Verificar el límite de horas semanales (RN-PER-04) contra el Código de Trabajo dominicano.
