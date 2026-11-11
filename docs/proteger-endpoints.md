# Cómo proteger endpoints (GI-97, GI-127)

Guía corta para los demás equipos: qué política poner en cada endpoint y qué devuelven los errores 401 y 403.

## Cómo usarlo

Pon siempre una política de `Politicas` (GIRA.Domain/Constants). **No escribas nombres de rol a mano** en los controladores.

```csharp
[Authorize(Policy = Politicas.GestionarInventario)]
[HttpPost]
public async Task<IActionResult> Crear(...) { ... }
```

- Un endpoint sin `[Authorize]` y sin `[AllowAnonymous]` queda abierto: no lo dejes así.
- `[AllowAnonymous]` solo se usa en login y refresh.
- Para un permiso nuevo o para cambiar quién tiene uno, se edita solo `GIRA.Api/Authorization/AutorizacionExtensions.cs` y se avisa en el PR.

## Políticas y roles

| Política | Roles permitidos | Módulo |
|---|---|---|
| `GestionarUsuarios` | Administrador | Seguridad |
| `OperarReservas` | Administrador, Gerente, Supervisor, Mesero | Reservas |
| `GestionarSalon` | Administrador, Gerente | Reservas |
| `ConsultarInventario` | Administrador, Gerente, Supervisor, Cocina | Inventario |
| `GestionarInventario` | Administrador, Gerente | Inventario |
| `RegistrarMovimientos` | Administrador, Gerente, Supervisor, Cocina | Inventario |
| `AtenderAlertasInventario` | Administrador, Gerente, Supervisor | Inventario |
| `GestionarPersonal` | Administrador, Gerente | Personal |
| `AsignarTurnos` | Administrador, Gerente, Supervisor | Personal |
| `ConsultarMisTurnos` | Todos los roles | Personal |
| `VerDashboard` | Administrador, Gerente | Afluencia |
| `VerPrediccion` | Administrador, Gerente, Supervisor | Afluencia |
| `GestionarModelos` | Administrador, Gerente | Afluencia |
| `GestionarConfiguracion` | Administrador, Gerente | Configuración |
| `GestionarPlantillasCorreo` | Administrador | Configuración |

> `AddAutorizacionGira` (en `GIRA.Api/Authorization/AutorizacionExtensions.cs`) es compartido entre todos los módulos: cada módulo agrega ahí sus propias políticas, no crea su propio registro de autorización.

## Endpoints actuales

| Endpoint | Verbo | Acceso |
|---|---|---|
| `/api/auth/login` | POST | Anónimo (máximo 10 intentos por minuto por IP; 429 si se pasa) |
| `/api/auth/refresh` | POST | Anónimo |
| `/api/auth/logout` | POST | `[Authorize]` (usuario autenticado, sin política específica) |
| `/api/auth/cambiar-password` | PUT | `[Authorize]` (usuario autenticado, sin política específica) |
| `/api/auth/me` | GET | `[Authorize]` (usuario autenticado, sin política específica) |
| `/api/roles` | GET | `GestionarUsuarios` |
| `/api/usuarios` | GET | `GestionarUsuarios`. Filtros opcionales por `UsuarioFiltroRequest` |
| `/api/usuarios/{id}` | GET | `GestionarUsuarios` |
| `/api/usuarios` | POST | `GestionarUsuarios` |
| `/api/usuarios/{id}` | PUT | `GestionarUsuarios` |
| `/api/usuarios/{id}/estado` | PATCH | `GestionarUsuarios` |
| `/api/usuarios/{id}/roles` | PUT | `GestionarUsuarios` |
| `/api/usuarios/{id}/accesos` | GET | `GestionarUsuarios`. Paginado (`page`, `pageSize` máximo 100), lo más reciente primero; filtros opcionales `exitoso`, `desde`, `hasta` |

### Reservas: agenda del personal (GI-164)

Todos con `OperarReservas` (Administrador, Gerente, Supervisor, Mesero). El estado y el origen salen como texto (por ejemplo `"Confirmada"`).

| Endpoint | Verbo | Acceso |
|---|---|---|
| `/api/reservas` | GET | `OperarReservas`. Paginado (`page`, `pageSize` máximo 100); filtros opcionales `fecha`, `estado` (nombre), `franjaId` y `telefono` (10 dígitos). Sin `fecha` ni `telefono` devuelve el día de hoy |
| `/api/reservas/{id}` | GET | `OperarReservas`. Detalle con cliente, mesa, franja, estado y motivo de cancelación; 404 si no existe |
| `/api/reservas/{id}/check-in` | PATCH | `OperarReservas`. Body `{ "personasReales": n }`. Desde 30 minutos antes de la franja y hasta que termina; 400 si supera la capacidad de la mesa; 409 si el estado o la hora no lo permiten. Devuelve el detalle |
| `/api/clientes` | GET | `OperarReservas`. `buscar` (nombre o teléfono, mínimo 3 caracteres), paginado |

### Reservas: zonas, mesas y franjas (GI-163)

Todos con `GestionarSalon` (Administrador, Gerente). `{recurso}` es `zonas`, `mesas` o `franjas`.

| Endpoint | Verbo | Acceso |
|---|---|---|
| `/api/{recurso}` | GET | `GestionarSalon`. Paginado (`page`, `pageSize` máximo 100); filtro opcional `activa` (mesas también `zonaId`) |
| `/api/{recurso}/{id}` | GET | `GestionarSalon` |
| `/api/{recurso}` | POST | `GestionarSalon`. 409 si el nombre o número ya existe |
| `/api/{recurso}/{id}` | PUT | `GestionarSalon` |
| `/api/{recurso}/{id}/estado` | PATCH | `GestionarSalon`. Body `{ "activa": bool }`; 409 si hay reservas futuras vigentes (reasignarlas primero) |

## Respuestas de error

Las dos usan `application/problem+json`, igual que el resto de la API.

**401: sin token, token vencido o inválido** (incluye el encabezado `WWW-Authenticate: Bearer`):

```json
{
  "title": "No autenticado",
  "status": 401,
  "detail": "Debes iniciar sesión para acceder a este recurso."
}
```

**403: token válido, pero el rol no tiene permiso:**

```json
{
  "title": "Acceso denegado",
  "status": 403,
  "detail": "Tu rol no tiene permiso para realizar esta acción."
}
```

El front debe tratar el 401 como "volver al login" y el 403 como "mostrar mensaje sin cerrar sesión".

## Probar con Swagger

1. Ejecuta la API en Development y abre `/swagger`.
2. En `POST /api/auth/login` inicia sesión y copia el `accessToken`.
3. Botón **Authorize**: pega solo el token, sin la palabra `Bearer`, y pulsa Authorize.
4. Prueba los endpoints protegidos.

## Pruebas

- `AutorizacionPoliticasTests`: 401 y 403 con endpoints de ejemplo que solo existen en las pruebas (`GiraApiFactoryConPruebas`).
- Para probar tu propio endpoint protegido: crea un usuario con `CrearUsuarioAsync`, inicia sesión con `ObtenerTokenAsync` y manda el token con `UsarToken`.