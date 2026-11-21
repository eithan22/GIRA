# Pruebas manuales — Módulo de Seguridad (GI-7)

Guía para verificar a mano que todo el módulo de Seguridad funciona: autenticación, sesión, autorización por rol, gestión de usuarios, roles, cambio y recuperación de contraseña, e historial de accesos.

Todas las 68 historias/subtareas de GI-7 están en Listo salvo **GI-133** (vincular usuario con empleado), bloqueada porque el módulo Personal (GI-92) aún no existe.

## 1. Preparar el entorno

```bash
# Terminal 1 — API (aplica migraciones y seeder al arrancar)
cd C:\Users\Lenovo\source\repos\GIRA
dotnet run --project GIRA.Api

# Terminal 2 — Frontend
cd C:\Users\Lenovo\source\repos\GIRA\Gira.frontend
npm run dev
```

Si usas Visual Studio al mismo tiempo, ciérralo o detén la depuración antes de correr `dotnet run`/`dotnet build` por consola: mantiene bloqueados los DLL y el build falla con `MSB3021`.

| Servicio | URL |
|---|---|
| API (HTTP) | `http://localhost:5253` |
| API (HTTPS) | `https://localhost:7177` |
| Swagger | `https://localhost:7177/swagger` |
| Frontend | `http://localhost:5173` |

**Admin sembrado:** el correo y la contraseña están en tus User Secrets (`Seed:AdminEmail`, `Seed:AdminPassword`):

```bash
cd GIRA.Api
dotnet user-secrets list
```

Los ejemplos de abajo usan `curl` contra `http://localhost:5253` (evita el lío del certificado self-signed de HTTPS en local). Si prefieres clic en vez de terminal, usa Swagger — todos los endpoints están documentados ahí con el botón **Authorize** para pegar el token.

---

## 2. Mapa de endpoints

| Método | Ruta | Auth | Para qué |
|---|---|---|---|
| POST | `/api/auth/login` | Anónimo (rate limit 10/min/IP) | Iniciar sesión |
| POST | `/api/auth/refresh` | Anónimo | Renovar tokens |
| POST | `/api/auth/logout` | Autenticado | Cerrar sesión |
| GET | `/api/auth/me` | Autenticado | Datos del usuario actual |
| PUT | `/api/auth/cambiar-password` | Autenticado | Cambiar mi contraseña |
| POST | `/api/auth/olvide-password` | Anónimo (rate limit 3/10min/IP) | Pedir recuperación |
| GET | `/api/usuarios` | Administrador | Listar usuarios (paginado, filtro rol/estado) |
| GET | `/api/usuarios/{id}` | Administrador | Ver un usuario |
| POST | `/api/usuarios` | Administrador | Crear usuario (password temporal) |
| PUT | `/api/usuarios/{id}` | Administrador | Editar nombre/correo/empleado |
| PATCH | `/api/usuarios/{id}/estado` | Administrador | Activar/desactivar |
| PUT | `/api/usuarios/{id}/roles` | Administrador | Reemplazar roles |
| POST | `/api/usuarios/{id}/resetear-password` | Administrador | Resetear contraseña de otro usuario |
| GET | `/api/usuarios/solicitudes-recuperacion` | Administrador | Cola de "olvidé mi contraseña" |
| DELETE | `/api/usuarios/solicitudes-recuperacion/{usuarioId}` | Administrador | Descartar una solicitud |
| GET | `/api/usuarios/{id}/accesos` | Administrador | Historial de inicios de sesión |
| GET | `/api/roles` | Administrador | Catálogo de roles |

Roles del sistema: `Administrador`, `Gerente`, `Supervisor`, `Mesero`, `Cocina`. Política de contraseña: mínimo 8 caracteres, mayúscula, minúscula, dígito y símbolo. Bloqueo: 5 intentos fallidos → 15 minutos.

---

## 3. Pruebas por API (curl)

### 3.1 Login — camino feliz

```bash
curl -s -X POST http://localhost:5253/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"TU_ADMIN","password":"TU_PASSWORD"}'
```
**Esperado:** 200, con `accessToken`, `refreshToken`, `usuario.roles: ["Administrador"]`. Guarda el `accessToken` en una variable para los siguientes pasos:
```bash
TOKEN=$(curl -s -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"TU_ADMIN","password":"TU_PASSWORD"}' | node -e "process.stdin.on('data',d=>console.log(JSON.parse(d).accessToken))")
REFRESH=$(curl -s -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"TU_ADMIN","password":"TU_PASSWORD"}' | node -e "process.stdin.on('data',d=>console.log(JSON.parse(d).refreshToken))")
```

### 3.2 Login — casos de error (los 4 deben dar 401 con el MISMO mensaje genérico)

```bash
# Correo inexistente
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"no-existe@gira.local","password":"loquesea"}'
# Contraseña incorrecta
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"TU_ADMIN","password":"incorrecta"}'
```
**Esperado:** ambos 401, mismo texto `"Correo o contraseña incorrectos."` — es intencional (RF-SEG-01, anti-enumeración), documentado en el comentario de GI-95.

### 3.3 Bloqueo por intentos fallidos

Crea o usa un usuario de prueba que NO sea tu único admin. Falla el login 5 veces seguidas con la contraseña mal:
```bash
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w "intento $i: %{http_code}\n" -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"usuario.prueba@gira.local","password":"mala"}'
done
```
**Esperado:** los 5 dan 401. Si intentas un 6º login, aunque pongas la contraseña CORRECTA, debe seguir dando 401 (cuenta bloqueada 15 min).

### 3.4 Rate limiting de login

```bash
for i in $(seq 1 11); do
  curl -s -o /dev/null -w "intento $i: %{http_code}\n" -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"x@x.com","password":"x"}'
done
```
**Esperado:** los primeros 10 dan 401, el 11º da **429**.

### 3.5 Sesión — /me, refresh, logout

```bash
echo "--- /me ---"
curl -s http://localhost:5253/api/auth/me -H "Authorization: Bearer $TOKEN"

echo "--- refresh ---"
curl -s -X POST http://localhost:5253/api/auth/refresh -H "Content-Type: application/json" -d "{\"refreshToken\":\"$REFRESH\"}"
# Guarda los tokens NUEVOS que devuelve esta llamada.

echo "--- reusar el refresh VIEJO (debe fallar) ---"
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/refresh -H "Content-Type: application/json" -d "{\"refreshToken\":\"$REFRESH\"}"

echo "--- logout ---"
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/logout -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"refreshToken\":\"$REFRESH\"}"
```
**Esperado:** `/me` → 200 con tus datos. `refresh` → 200 con tokens distintos a los anteriores. Reusar el refresh viejo → 401 (rotación real, no se puede reutilizar). `logout` → 204.

### 3.6 Autorización por rol (401 / 403)

```bash
echo "--- sin token ---"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5253/api/usuarios

echo "--- con token basura ---"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5253/api/usuarios -H "Authorization: Bearer token-invalido"
```
**Esperado:** ambos 401. Luego, loguéate como un usuario con rol **Mesero** o **Gerente** (no Administrador) y repite contra `/api/usuarios`, `/api/roles`, `/api/usuarios/{id}/accesos` → **403** en todos. `/api/auth/me` con ese mismo token sí debe dar 200 (cualquier rol puede ver sus propios datos).

### 3.7 Gestión de usuarios

```bash
echo "--- crear ---"
curl -s -X POST http://localhost:5253/api/usuarios -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"email":"prueba.manual@gira.local","nombreCompleto":"Prueba Manual","empleadoId":null}'
# Guarda el "id" y la "passwordTemporal" de la respuesta.

echo "--- listar con filtro ---"
curl -s "http://localhost:5253/api/usuarios?rol=Mesero&activo=true" -H "Authorization: Bearer $TOKEN"

echo "--- obtener por id ---"
curl -s "http://localhost:5253/api/usuarios/EL_ID" -H "Authorization: Bearer $TOKEN"

echo "--- desactivar ---"
curl -s -o /dev/null -w "%{http_code}\n" -X PATCH "http://localhost:5253/api/usuarios/EL_ID/estado" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"activo":false}'

echo "--- login del usuario recién desactivado (debe fallar) ---"
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/login -H "Content-Type: application/json" -d '{"email":"prueba.manual@gira.local","password":"LA_PASSWORD_TEMPORAL"}'
```
**Esperado:** crear → 200/201 con password temporal. Login del usuario desactivado → 401.

**Regla del último Administrador** (GI-132): intenta desactivar tu ÚNICO admin, o quitarle el rol Administrador vía `PUT /api/usuarios/{id}/roles`:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X PATCH "http://localhost:5253/api/usuarios/TU_PROPIO_ID/estado" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"activo":false}'
```
**Esperado:** **409 Conflict** — no debe dejarte sin administradores activos.

### 3.8 Cambiar mi contraseña

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X PUT http://localhost:5253/api/auth/cambiar-password -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"passwordActual":"TU_PASSWORD_ACTUAL","passwordNueva":"NuevaClave1@"}'
```
**Esperado:** 204. Un login con la contraseña vieja debe fallar (401); con la nueva, funcionar.

### 3.9 Recuperar contraseña (anónimo)

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5253/api/auth/olvide-password -H "Content-Type: application/json" -d '{"email":"prueba.manual@gira.local"}'
curl -s "http://localhost:5253/api/usuarios/solicitudes-recuperacion" -H "Authorization: Bearer $TOKEN"
```
**Esperado:** `olvide-password` → 200 vacío (igual si el correo existe o no). La solicitud aparece en la cola del admin. Repite la llamada 4 veces seguidas: la 4ª debe dar **429**.

### 3.10 Historial de accesos

```bash
curl -s "http://localhost:5253/api/usuarios/EL_ID/accesos?exitoso=false" -H "Authorization: Bearer $TOKEN"
```
**Esperado:** 200, con los intentos fallidos registrados (correo inexistente, contraseña incorrecta, bloqueado, inactivo — cada uno con su motivo).

---

## 4. Pruebas desde la interfaz web

1. Entra a `http://localhost:5173/login` y loguéate con el admin. Debe llevarte a `/seguridad/usuarios`.
2. **Usuarios:** verifica el contador de usuarios, la lista paginada, el filtro por rol/estado.
3. **Crear usuario** (`/seguridad/usuarios/nuevo`): llena el formulario, confirma que te muestra la contraseña temporal una sola vez.
4. **Editar usuario** (`/seguridad/usuarios/:id/editar`): cambia nombre/correo, guarda, confirma que se refleja en la lista.
5. **Detalle de usuario** (`/seguridad/usuarios/:id`): revisa que se vean sus últimos accesos.
6. **Activar/desactivar**: desde el detalle o la lista, desactiva un usuario de prueba y confirma el cambio de estado visual.
7. **Asignar roles**: usa el selector de roles, guarda, confirma que el usuario cambió de rol en la lista.
8. **Cambiar mi contraseña** (`/perfil/cambiar-password`): cambia tu propia contraseña y vuelve a loguearte con la nueva.
9. **Olvidé mi contraseña** (`/recuperar-password`, sin sesión): pide la recuperación y confirma que aparece en "Solicitudes de contraseña" dentro de Usuarios, con el badge de pendientes en el menú.
10. **Cerrar sesión**: usa "Cerrar sesión" en el menú, confirma que te regresa a `/login` y que no puedes volver atrás sin loguearte.
11. **Acceso denegado**: loguéate con un usuario no-Administrador (Mesero/Gerente) e intenta entrar a `/seguridad/usuarios` directamente por URL — debe mandarte a `/acceso-denegado` (403), y el menú lateral no debe mostrar la opción de Usuarios.

---

## 5. Checklist rápido

- [ ] Login correcto devuelve tokens y rol
- [ ] Credenciales inválidas → 401 genérico (igual para correo inexistente, password mala, bloqueado, inactivo)
- [ ] 5 fallos seguidos bloquean la cuenta 15 min
- [ ] Rate limit de login (10/min) y de olvide-password (3/10min) responden 429
- [ ] `/me`, `refresh`, `logout` funcionan; el refresh viejo no se puede reusar
- [ ] Endpoints de Administrador dan 401 sin token y 403 con rol equivocado
- [ ] CRUD de usuarios completo (crear, listar+filtro, obtener, editar, activar/desactivar)
- [ ] No se puede desactivar ni quitarle el rol al último Administrador activo (409)
- [ ] Cambio de contraseña propio funciona y revoca sesiones
- [ ] Recuperación de contraseña: solicitud, cola del admin, reseteo administrativo
- [ ] Historial de accesos registra cada intento con su motivo
- [ ] UI: todos los flujos de la sección 4 sin errores en consola del navegador
