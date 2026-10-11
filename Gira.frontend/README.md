# GIRA · Frontend de seguridad

Interfaz React para inicio de sesión, autorización por roles y administración de usuarios.

## Inicio rápido

```bash
npm install
Copy-Item .env.example .env
npm run dev
```

Configura `VITE_API_BASE_URL` en `.env` con la URL base de la API. El cliente espera los endpoints:

- `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
- `GET /api/auth/me`, `POST /api/auth/change-password`
- `GET /api/users`, `GET /api/users/{id}`, `POST /api/users`, `PUT /api/users/{id}`
- `PATCH /api/users/{id}/status`, `GET /api/users/{id}/accesses`
- `GET /api/roles`

El login consume `email` y `password`; la respuesta de autenticación debe incluir `accessToken` (o `token`), `refreshToken` y el usuario, o permitir recuperarlo con `/api/auth/me`. Las listas paginadas aceptan `page`, `pageSize`, `search`, `role` y `activo`. Ajusta los mapeos en `src/security/authService.js` si los DTO del backend usan otros nombres.

## Modo demostración

Los botones de roles de la pantalla de login usan datos locales únicamente para demostraciones. Las cuentas, cambios y accesos demo se guardan en `localStorage`; el login normal siempre consulta la API. Cerrar sesión elimina la sesión y conserva los datos locales de demostración.

## Validación

```bash
npm run lint
npm run build
```
