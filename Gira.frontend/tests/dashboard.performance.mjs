import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { createServer } from 'vite';

if (!process.env.GIRA_BENCH_API || !process.env.GIRA_BENCH_TOKEN) throw new Error('Falta el entorno aislado de rendimiento.');
process.env.VITE_API_BASE_URL = '';
const server = await createServer({
  server: { host: '127.0.0.1', port: 0, proxy: { '/api': { target: process.env.GIRA_BENCH_API, changeOrigin: true } } },
  logLevel: 'error',
});
let browser;
try {
  await server.listen();
  browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_CHROME_EXECUTABLE_PATH || undefined });
  const page = await browser.newPage();
  await page.addInitScript((token) => localStorage.setItem('gira.accessToken', token), process.env.GIRA_BENCH_TOKEN);
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: { id: 'prueba', nombre: 'Gerente', roles: ['Gerente'], activo: true } }));
  await page.goto(`http://127.0.0.1:${server.httpServer.address().port}/gerencia/dashboard`);
  await page.getByRole('heading', { name: 'Dashboard gerencial' }).waitFor();
  const hasta = await page.getByLabel('Hasta', { exact: true }).inputValue();
  const start = new Date(`${hasta}T00:00:00Z`);
  start.setUTCDate(start.getUTCDate() - 364);
  const desde = start.toISOString().slice(0, 10);
  await page.getByLabel('Desde', { exact: true }).fill(desde);
  await page.getByRole('button', { name: 'Aplicar', exact: true }).click();
  await page.waitForFunction(() => !document.querySelector('.page-heading-row button')?.disabled);
  const tiempos = [];
  let filas = 0;
  for (let i = 0; i < 5; i++) {
    const paths = ['/api/analitica/kpis', '/api/analitica/prediccion', '/api/analitica/recomendaciones', '/api/inventario/alertas'];
    const respuestas = paths.map((path) => page.waitForResponse((r) => r.url().includes(path) && r.request().method() === 'GET'));
    const historial = page.waitForResponse((r) => r.url().includes('/api/analitica/historial')
      && r.url().includes(`desde=${desde}`) && r.request().method() === 'GET');
    const historialPrediccion = page.waitForResponse((r) => r.url().includes('/api/analitica/historial')
      && !r.url().includes(`desde=${desde}`) && r.request().method() === 'GET');
    const comienzo = Date.now();
    await page.getByRole('button', { name: 'Actualizar', exact: true }).click();
    const [historia, historiaPrediccion, ...resto] = await Promise.all([historial, historialPrediccion, ...respuestas]);
    assert.ok([historia, historiaPrediccion, ...resto].every((r) => r.status() === 200), 'Alguna consulta del dashboard falló.');
    filas = (await historia.json()).length;
    assert.equal((await resto[0].json()).comensalesAtendidos, 29200, 'El KPI no refleja todos los registros.');
    await page.waitForFunction(() => !document.querySelector('.page-heading-row button')?.disabled
      && !document.querySelector('.dashboard-predicciones [role=status]')
      && !document.querySelector('.dashboard-proximamente [role=status]'));
    assert.equal(await page.locator('.dashboard [role=alert]').count(), 0, 'El dashboard mostró un error.');
    tiempos.push(Date.now() - comienzo);
  }
  assert.equal(filas, 1460, 'El historial no contiene las 365 fechas y cuatro franjas previstas.');
  console.log(JSON.stringify({ desde, hasta, registros: 14600, filas, medicionesMs: tiempos }));
  assert.ok(tiempos.every((t) => t < 3000), 'El dashboard superó los 3 segundos.');
} finally {
  await browser?.close();
  await server.close();
}
