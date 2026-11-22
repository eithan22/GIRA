import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { createServer } from 'vite';

let server;
let browser;
let base;

before(async () => {
  process.env.VITE_API_BASE_URL = '';
  server = await createServer({ server: { host: '127.0.0.1', port: 0, proxy: {} }, logLevel: 'error' });
  await server.listen();
  base = `http://127.0.0.1:${server.httpServer.address().port}`;
  browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_CHROME_EXECUTABLE_PATH || undefined });
});

after(async () => {
  await browser?.close();
  await server?.close();
});

async function pagina(role = 'Gerente') {
  const context = await browser.newContext();
  const page = await context.newPage();
  if (role) await page.addInitScript(() => localStorage.setItem('gira.accessToken', 'token-de-prueba'));
  let fallar = false;
  let fallarKpis = false;
  let demorarPrediccion = false;
  let vacio = false;
  let descartada = false;
  let primeraFecha = null;
  let feriados = [];
  let modelos = [
    { id: 'modelo-vigente', franjaHorariaId: 'franja-prueba', franja: 'Almuerzo', version: 'ssa-vigente',
      entrenamientoDesde: '2026-09-01', entrenamientoHasta: '2026-10-26',
      validacionDesde: '2026-10-27', validacionHasta: '2026-11-09',
      mae: 2, rmse: 3, creadoEn: '2026-11-10T12:00:00Z', activo: true },
    { id: 'modelo-anterior', franjaHorariaId: 'franja-prueba', franja: 'Almuerzo', version: 'ssa-anterior',
      entrenamientoDesde: '2026-08-01', entrenamientoHasta: '2026-09-25',
      validacionDesde: '2026-09-26', validacionHasta: '2026-10-09',
      mae: 3, rmse: 4, creadoEn: '2026-10-10T12:00:00Z', activo: false },
  ];
  let trabajoEntrenamiento = null;
  let entrenamientoSinDatos = false;
  const consultas = [];
  const consultasPrediccion = [];
  let variasFranjas = false;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    consultas.push(url.pathname);
    const desde = url.searchParams.get('desde');
    const hasta = url.searchParams.get('hasta');
    let data;
    let status = 200;
    if (url.pathname === '/api/auth/me') {
      data = { id: 'usuario-prueba', nombre: 'Gerente', roles: [role], activo: true };
    } else if (url.pathname === '/api/analitica/kpis') {
      status = fallarKpis ? 503 : 200;
      data = { desde, hasta, comensalesAtendidos: 21, tasaOcupacion: null, tasaNoShow: 0,
        tasaCancelacion: null, proporcionWalkIns: null, precisionModelo: null,
        costoInsumosPorComensal: null, merma: null, alertasAbiertas: 0,
        horasPersonalPorComensal: null, afluenciaPromedio: [] };
    } else if (url.pathname === '/api/analitica/prediccion') {
      if (demorarPrediccion) await new Promise((resolve) => setTimeout(resolve, 400));
      primeraFecha ??= desde;
      consultasPrediccion.push({ desde, hasta, franjaId: url.searchParams.get('franjaId') });
      status = fallar ? 500 : 200;
      const predicciones = [{
        fecha: desde, franjaHorariaId: 'franja-prueba', franja: 'Almuerzo',
        afluenciaEstimada: desde === primeraFecha ? 19 : 27, limiteInferior: null, limiteSuperior: null,
        bajaConfianza: true, generadaEn: '2026-11-20T12:00:00Z', versionModelo: 'base-prueba',
      }];
      if (variasFranjas) predicciones.push({
        fecha: desde, franjaHorariaId: 'franja-cena', franja: 'Cena',
        afluenciaEstimada: 8, limiteInferior: null, limiteSuperior: null,
        bajaConfianza: false, generadaEn: '2026-11-20T12:00:00Z', versionModelo: 'ssa-prueba',
      });
      data = fallar ? { detail: 'detalle interno que no debe mostrarse' }
        : vacio ? []
          : predicciones.filter((prediccion) => !url.searchParams.get('franjaId')
            || prediccion.franjaHorariaId === url.searchParams.get('franjaId'));
    } else if (url.pathname === '/api/analitica/historial') {
      data = desde === hasta ? [{ fecha: desde, franjaHorariaId: 'franja-prueba', totalComensales: 7 }] : [];
    } else if (url.pathname === '/api/analitica/recomendaciones' && request.method() === 'GET') {
      data = descartada ? [] : [{ id: 'recomendacion-prueba', fecha: desde || '2026-11-20', tipo: 'Analítica',
        mensaje: 'Se necesitan más datos.', prioridad: 'Media', estado: 'Nueva', origen: 'Predicción',
        fechaGeneracion: '2026-11-20T12:00:00Z' }];
    } else if (url.pathname === '/api/analitica/recomendaciones/recomendacion-prueba' && request.method() === 'PATCH') {
      descartada = true;
      status = 204;
    } else if (url.pathname === '/api/analitica/modelos' && request.method() === 'GET') {
      data = modelos;
    } else if (url.pathname === '/api/analitica/modelos/entrenamientos' && request.method() === 'POST') {
      status = 202;
      data = { id: 'entrenamiento-prueba', estado: 'EnCola' };
      trabajoEntrenamiento = {
        id: 'entrenamiento-prueba', estado: 'Completado',
        solicitadoEn: '2026-11-20T12:00:00Z', iniciadoEn: '2026-11-20T12:01:00Z',
        finalizadoEn: '2026-11-20T12:02:00Z', error: null,
        resultados: entrenamientoSinDatos
          ? [{ franjaHorariaId: 'franja-prueba', franja: 'Almuerzo', estado: 'DatosInsuficientes',
            observacionesEntrenamiento: 0, observacionesValidacion: 0, mae: null, rmse: null,
            maeReferencia: null, versionModelo: null }]
          : [{ franjaHorariaId: 'franja-prueba', franja: 'Almuerzo', estado: 'Activado',
            observacionesEntrenamiento: 56, observacionesValidacion: 14, mae: 1, rmse: 2,
            maeReferencia: 2, versionModelo: 'ssa-nuevo' }],
      };
    } else if (url.pathname === '/api/analitica/modelos/entrenamientos/entrenamiento-prueba') {
      data = trabajoEntrenamiento;
    } else if (url.pathname === '/api/analitica/modelos/modelo-anterior/activar' && request.method() === 'POST') {
      modelos = modelos.map((modelo) => ({ ...modelo, activo: modelo.id === 'modelo-anterior' }));
      status = 204;
    } else if (url.pathname === '/api/feriados' && request.method() === 'GET') {
      data = feriados;
    } else if (url.pathname === '/api/feriados' && request.method() === 'POST') {
      const solicitud = request.postDataJSON();
      const feriado = { id: 'feriado-prueba', ...solicitud };
      feriados = [...feriados, feriado];
      data = { diaCerrado: feriado, advertencias: [] };
    } else if (url.pathname === '/api/feriados/feriado-prueba' && request.method() === 'DELETE') {
      feriados = feriados.filter((feriado) => feriado.id !== 'feriado-prueba');
      status = 204;
    } else if (url.pathname === '/api/inventario/alertas') {
      data = { items: [], total: 0, page: 1, pageSize: 5 };
    } else {
      status = 404;
      data = {};
    }
    await route.fulfill({ status, ...(status === 204 ? {} : { json: data }) });
  });
  return { context, page, consultas, consultasPrediccion,
    variasFranjas: (valor) => { variasFranjas = valor; },
    entrenamientoSinDatos: (valor) => { entrenamientoSinDatos = valor; },
    fallar: (valor) => { fallar = valor; }, vacio: (valor) => { vacio = valor; },
    fallarKpis: (valor) => { fallarKpis = valor; }, demorarPrediccion: (valor) => { demorarPrediccion = valor; } };
}

test('gerencia consulta KPI y predicciones, cambia fecha y maneja vacío, error y reintento', async () => {
  const prueba = await pagina();
  const { page, context } = prueba;
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByRole('heading', { name: 'Dashboard gerencial' }).waitFor();
    await page.getByText('Se necesitan más datos.').waitFor();
    await page.getByText('Afluencia real: 7 comensales').waitFor();
    assert.equal(await page.locator('.dashboard-stat').filter({ hasText: 'Comensales registrados' }).locator('strong').innerText(), '21');
    assert.equal(await page.getByText('Baja confianza', { exact: false }).count(), 1);
    const campo = page.getByLabel('Fecha de predicción o comparación');
    const anterior = await campo.inputValue();
    const siguiente = new Date(`${anterior}T00:00:00Z`);
    siguiente.setUTCDate(siguiente.getUTCDate() + 1);
    const nueva = siguiente.toISOString().slice(0, 10);
    prueba.demorarPrediccion(true);
    await campo.fill(nueva);
    await page.getByRole('button', { name: 'Consultar predicción' }).click();
    await page.getByText('Cargando predicción…').waitFor();
    await page.getByText('Fecha consultada:', { exact: false }).waitFor();
    await page.waitForFunction((d) => document.querySelector('.dashboard-predicciones')?.textContent.includes(d),
      `${nueva.slice(8, 10)}/${nueva.slice(5, 7)}/${nueva.slice(0, 4)}`);
    assert.equal(await page.getByText('Estimación: 19 comensales').count(), 0);
    await page.getByText('Estimación: 27 comensales').waitFor();
    prueba.demorarPrediccion(false);

    prueba.fallar(true);
    await page.getByRole('button', { name: 'Consultar predicción' }).click();
    await page.getByText('No se pudo consultar las predicciones. Intenta nuevamente.').waitFor();
    assert.equal(await page.getByText('Estimación: 27 comensales').count(), 0);
    assert.equal(await page.getByText('detalle interno que no debe mostrarse').count(), 0);
    prueba.fallar(false);
    prueba.vacio(true);
    await page.getByRole('button', { name: 'Reintentar predicción' }).click();
    await page.getByText('No hay predicciones disponibles para esta fecha.').waitFor();
    await page.getByRole('button', { name: 'Descartar', exact: true }).click();
    await page.getByText('No hay recomendaciones vigentes.').waitFor();
  } finally {
    await context.close();
  }
});

test('el período principal sincroniza la fecha de predicción sin impedir consultar otra fecha', async () => {
  const { page, context, consultasPrediccion } = await pagina();
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByText('Estimación: 19 comensales').waitFor();
    const hasta = await page.getByLabel('Hasta', { exact: true }).inputValue();
    const anterior = new Date(`${hasta}T00:00:00Z`);
    anterior.setUTCDate(anterior.getUTCDate() - 3);
    const fecha = anterior.toISOString().slice(0, 10);
    await page.getByLabel('Desde', { exact: true }).fill(fecha);
    await page.getByLabel('Hasta', { exact: true }).fill(fecha);
    await page.getByRole('button', { name: 'Aplicar', exact: true }).click();
    await page.waitForFunction((valor) =>
      document.querySelector('.dashboard-predicciones input[type="date"]')?.value === valor, fecha);
    await page.getByText('Estimación: 27 comensales').waitFor();
    assert.equal(consultasPrediccion.at(-1).desde, fecha);
    assert.equal(consultasPrediccion.at(-1).hasta, fecha);
    assert.equal(await page.getByText('Afluencia real: 7 comensales').count(), 1);
  } finally {
    await context.close();
  }
});

test('permite comparar un pronóstico histórico con la afluencia real de la misma fecha', async () => {
  const { page, context } = await pagina();
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByText('Estimación: 19 comensales').waitFor();
    const campo = page.getByLabel('Fecha de predicción o comparación');
    const fechaAyer = new Date(`${await campo.inputValue()}T00:00:00Z`);
    fechaAyer.setUTCDate(fechaAyer.getUTCDate() - 2);
    await campo.fill(fechaAyer.toISOString().slice(0, 10));
    await page.getByRole('button', { name: 'Consultar predicción' }).click();
    await page.getByText('Estimación: 27 comensales').waitFor();
    await page.getByText('Afluencia real: 7 comensales').waitFor();
  } finally {
    await context.close();
  }
});

test('filtra predicciones por franja usando el parámetro franjaId de la API', async () => {
  const prueba = await pagina();
  prueba.variasFranjas(true);
  const { page, context } = prueba;
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByText('Estimación: 19 comensales').waitFor();
    assert.equal(await page.locator('.dashboard-prediccion-lista > li').count(), 2);

    await page.getByLabel('Franja horaria').selectOption('franja-cena');
    await page.getByText('Estimación: 8 comensales').waitFor();
    assert.equal(await page.locator('.dashboard-prediccion-lista > li').count(), 1);
    assert.deepEqual(prueba.consultasPrediccion.slice(-1)[0], {
      desde: await page.getByLabel('Fecha de predicción o comparación').inputValue(),
      hasta: await page.getByLabel('Fecha de predicción o comparación').inputValue(),
      franjaId: 'franja-cena',
    });
  } finally {
    await context.close();
  }
});

test('un fallo de indicadores no muestra valores anteriores y permite reintentar', async () => {
  const prueba = await pagina();
  const { page, context } = prueba;
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    const tarjeta = page.locator('.dashboard-stat').filter({ hasText: 'Comensales registrados' }).locator('strong');
    await page.waitForFunction(() => [...document.querySelectorAll('.dashboard-stat')]
      .some((e) => e.textContent.includes('Comensales registrados') && e.querySelector('strong')?.textContent === '21'));
    prueba.fallarKpis(true);
    const hasta = await page.getByLabel('Hasta', { exact: true }).inputValue();
    await page.getByLabel('Desde', { exact: true }).fill(hasta);
    await page.getByRole('button', { name: 'Aplicar', exact: true }).click();
    await page.getByText('No se pudo consultar los indicadores. Intenta nuevamente.', { exact: false }).waitFor();
    assert.equal(await tarjeta.innerText(), '—');
    prueba.fallarKpis(false);
    await page.getByRole('button', { name: 'Reintentar', exact: true }).click();
    await page.waitForFunction(() => [...document.querySelectorAll('.dashboard-stat')]
      .some((e) => e.textContent.includes('Comensales registrados') && e.querySelector('strong')?.textContent === '21'));
  } finally {
    await context.close();
  }
});

test('sin sesión o con rol no autorizado no muestra el dashboard ni solicita sus datos', async () => {
  for (const role of [null, 'Supervisor']) {
    const { page, context, consultas } = await pagina(role);
    try {
      await page.goto(`${base}/gerencia/dashboard`);
      await page.waitForURL(role ? '**/acceso-denegado' : '**/login');
      assert.equal(await page.getByRole('heading', { name: 'Dashboard gerencial' }).count(), 0);
      assert.equal(consultas.some((url) => url.startsWith('/api/analitica/')), false);
    } finally {
      await context.close();
    }
  }
});

test('gerencia solicita entrenamiento, revisa métricas, registra feriado y audita rollback', async () => {
  const { page, context } = await pagina();
  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByRole('heading', { name: 'Modelos de afluencia' }).waitFor();

    await page.getByRole('button', { name: 'Solicitar entrenamiento' }).click();
    await page.getByText('Entrenamiento Completado').waitFor();
    await page.getByText('Almuerzo · Modelo activado', { exact: false }).waitFor();

    await page.getByLabel('Motivo del rollback').fill('La versión vigente empeoró.');
    await page.getByRole('button', { name: 'Reactivar versión' }).click();
    await page.getByText('Almuerzo · ssa-anterior · Activo').waitFor();

    const hasta = await page.getByLabel('Hasta', { exact: true }).inputValue();
    const futuro = new Date(`${hasta}T00:00:00Z`);
    futuro.setUTCDate(futuro.getUTCDate() + 5);
    await page.getByLabel('Fecha', { exact: true }).fill(futuro.toISOString().slice(0, 10));
    await page.getByLabel('Motivo', { exact: true }).fill('Feriado de prueba');
    await page.getByRole('button', { name: 'Agregar fecha' }).click();
    await page.getByText('El feriado se agregó al calendario.').waitFor();
    await page.getByRole('button', { name: 'Quitar', exact: true }).click();
    await page.getByRole('button', { name: 'Confirmar', exact: true }).click();
    await page.getByText('El feriado se quitó del calendario.').waitFor();
  } finally {
    await context.close();
  }
});

test('informa que no se entrenó un modelo cuando faltan observaciones', async () => {
  const prueba = await pagina();
  prueba.entrenamientoSinDatos(true);
  try {
    await prueba.page.goto(`${base}/gerencia/dashboard`);
    await prueba.page.getByRole('button', { name: 'Solicitar entrenamiento' }).click();
    await prueba.page.getByText('Entrenamiento finalizado: datos insuficientes en todas las franjas').waitFor();
    await prueba.page.getByText('Almuerzo · Datos insuficientes').waitFor();
  } finally {
    await prueba.context.close();
  }
});

test('el dashboard es responsive y sus acciones se operan por teclado', async () => {
  const { page, context } = await pagina();
  const erroresConsola = [];
  page.on('console', (mensaje) => {
    if (mensaje.type() === 'error') erroresConsola.push(mensaje.text());
  });
  page.on('pageerror', (error) => erroresConsola.push(error.message));

  try {
    await page.goto(`${base}/gerencia/dashboard`);
    await page.getByRole('heading', { name: 'Dashboard gerencial' }).waitFor();
    await page.addStyleTag({ content: 'html { scrollbar-gutter: stable; }' });

    for (const viewport of [
      { width: 360, height: 800 },
      { width: 768, height: 1024 },
      { width: 1366, height: 768 },
    ]) {
      await page.setViewportSize(viewport);
      const resultado = await page.evaluate(() => {
        const rectangulos = [...document.querySelectorAll('.dashboard-stats .dashboard-stat')]
          .map((elemento) => elemento.getBoundingClientRect().toJSON());
        const controles = [...document.querySelectorAll('.dashboard input, .dashboard button, .dashboard a')]
          .filter((elemento) => elemento.getClientRects().length > 0)
          .map((elemento) => elemento.getBoundingClientRect().toJSON());
        return {
          anchoDocumento: document.documentElement.scrollWidth,
          anchoVentana: document.documentElement.clientWidth,
          fueraDePantalla: controles.some((rectangulo) =>
            rectangulo.left < 0 || rectangulo.right > document.documentElement.clientWidth),
          navegacionFuera: window.innerWidth <= 760 && [...document.querySelectorAll('.side-nav .nav-link')]
            .some((elemento) => {
              const rectangulo = elemento.getBoundingClientRect();
              return rectangulo.left < 0 || rectangulo.right > document.documentElement.clientWidth;
            }),
          seSuperponen: rectangulos.some((a, indice) => rectangulos.slice(indice + 1).some((b) =>
            Math.min(a.right, b.right) > Math.max(a.left, b.left)
            && Math.min(a.bottom, b.bottom) > Math.max(a.top, b.top))),
        };
      });

      assert.ok(resultado.anchoDocumento <= resultado.anchoVentana, `Desbordamiento horizontal en ${viewport.width}px`);
      assert.equal(resultado.fueraDePantalla, false, `Control fuera de pantalla en ${viewport.width}px`);
      assert.equal(resultado.navegacionFuera, false, `Navegación fuera de pantalla en ${viewport.width}px`);
      assert.equal(resultado.seSuperponen, false, `Tarjetas superpuestas en ${viewport.width}px`);
    }

    const fechaPrediccion = page.getByLabel('Fecha de predicción o comparación');
    await fechaPrediccion.focus();
    const accion = page.getByRole('button', { name: 'Consultar predicción' });
    for (let intento = 0; intento < 5
      && !await accion.evaluate((elemento) => elemento === document.activeElement); intento++) {
      await page.keyboard.press('Tab');
    }
    assert.equal(await accion.evaluate((elemento) => elemento === document.activeElement), true,
      `El foco quedó en ${await page.evaluate(() => document.activeElement?.outerHTML)}`);
    assert.ok(Number.parseFloat(await accion.evaluate((elemento) => getComputedStyle(elemento).outlineWidth)) >= 2);
    await page.keyboard.press('Enter');
    await page.getByText('Fecha consultada:', { exact: false }).waitFor();
    assert.deepEqual(erroresConsola, []);
  } finally {
    await context.close();
  }
});
