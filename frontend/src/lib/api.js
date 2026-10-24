const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

function buildUrl(path) {
  if (!API_BASE_URL) {
    return path.startsWith('/') ? path : `/${path}`
  }
  const base = API_BASE_URL.replace(/\/+$/, '')
  const cleanPath = path.replace(/^\/+/, '')
  return `${base}/${cleanPath}`
}

async function parseResponse(response) {
  const contentType = response.headers.get('content-type')
  const isJson = contentType && contentType.includes('application/json')

  if (response.status === 204 || response.status === 205) {
    return null
  }

  if (!isJson) {
    const text = await response.text()
    return text ? text : null
  }

  return response.json()
}

export class ApiError extends Error {
  constructor(message, status, data) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.data = data
  }
}

async function request(method, path, options = {}) {
  const { body, token, headers = {}, ...rest } = options

  const requestHeaders = {
    'Content-Type': 'application/json',
    ...headers,
  }

  if (token) {
    requestHeaders.Authorization = `Bearer ${token}`
  }

  const config = {
    method,
    headers: requestHeaders,
    ...rest,
  }

  if (body !== undefined && body !== null) {
    config.body = JSON.stringify(body)
  }

  const response = await fetch(buildUrl(path), config)
  const data = await parseResponse(response)

  if (!response.ok) {
    const message =
      (data && typeof data === 'object' && (data.message || data.error || data.Message || data.Error)) ||
      response.statusText ||
      `HTTP ${response.status}`
    throw new ApiError(message, response.status, data)
  }

  return data
}

export const api = {
  get: (path, options) => request('GET', path, options),
  post: (path, body, options) => request('POST', path, { body, ...options }),
  put: (path, body, options) => request('PUT', path, { body, ...options }),
  patch: (path, body, options) => request('PATCH', path, { body, ...options }),
  delete: (path, options) => request('DELETE', path, options),
}

export default api