# Dashboard gerencial GIRA

Interfaz React + Vite para consultar las predicciones de afluencia y los KPIs del módulo de Analítica.

## Desarrollo local

1. Inicia SQL Server con Docker desde la raíz de `GIRA` y configura `ConnectionStrings:GiraDb` y los secretos iniciales de Identity en User Secrets para `GIRA.Api`.
2. Inicia la API con el perfil HTTP (`http://localhost:5253`). En Development aplica las migraciones pendientes de `GiraDbContext`.
3. Desde `Gira.frontend`, ejecuta:

   ```sh
   npm install
   npm run dev
   ```

Vite reenvía `/api` a `http://localhost:5253`. Si la API escucha en otra URL, define `GIRA_API_URL` al iniciar Vite. Para conectarse sin proxy, define `VITE_API_BASE_URL` con la URL base que termina en `/api`.

La interfaz consume estos endpoints:

- `GET /api/analitica/kpis`
- `GET /api/analitica/prediccion?fecha=YYYY-MM-DD`
- `GET /api/analitica/historial?desde=YYYY-MM-DD&hasta=YYYY-MM-DD`

El dashboard no genera datos de demostración: muestra el estado vacío y los errores de la API cuando no hay historial o predicciones disponibles.

## Integraciones pendientes del resto de módulos

- Reservas debe publicar `ReservaConfirmadaEvent` después de confirmar y guardar la reserva para alimentar el historial automáticamente.
- El árbol de Seguridad disponible en esta rama aporta Identity y su persistencia, pero no configura autenticación JWT ni políticas de autorización. Los endpoints de Analítica todavía deben protegerse con los roles Gerente/Administrador antes de exponer la API fuera del entorno local.
