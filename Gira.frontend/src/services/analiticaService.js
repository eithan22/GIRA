const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

async function request(path, signal) {
  let response

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      headers: { Accept: 'application/json' },
      signal,
    })
  } catch (error) {
    if (error.name === 'AbortError') {
      throw error
    }

    throw new Error('No se pudo conectar con la API de Analítica. Verifica que el backend esté ejecutándose.')
  }

  if (!response.ok) {
    let detail = `La API respondió con el estado ${response.status}.`
    try {
      const problem = await response.json()
      detail = problem.detail || problem.title || detail
    } catch {
      // Si el servidor no devuelve ProblemDetails, se conserva el mensaje HTTP.
    }

    throw new Error(detail)
  }

  return response.json()
}

export const getKpis = (signal) =>
  request('/analitica/kpis', signal)

export const getPrediction = (date, signal) =>
  request(`/analitica/prediccion?fecha=${encodeURIComponent(date)}`, signal)

export const getHistory = (from, to, signal) =>
  request(
    `/analitica/historial?desde=${encodeURIComponent(from)}&hasta=${encodeURIComponent(to)}`,
    signal,
  )
