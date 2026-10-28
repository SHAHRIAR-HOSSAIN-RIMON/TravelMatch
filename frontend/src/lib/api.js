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

/**
 * Human-readable message for a failed request.
 * Covers the backend's result envelopes (data.message), the ASP.NET Core
 * ValidationProblemDetails body (data.errors / data.title) and bare string bodies.
 */
export function describeApiError(error, fallback = 'Something went wrong. Please try again.') {
  if (error instanceof ApiError) {
    const data = error.data

    if (data && typeof data === 'object') {
      if (data.errors && typeof data.errors === 'object' && !Array.isArray(data.errors)) {
        const firstMessage = Object.values(data.errors).flat().find(Boolean)
        if (firstMessage) return String(firstMessage)
      }

      if (typeof data.message === 'string' && data.message.trim()) return data.message.trim()
      if (typeof data.title === 'string' && data.title.trim()) return data.title.trim()
    }

    if (typeof data === 'string' && data.trim()) return data.trim()

    return error.message || fallback
  }

  if (error instanceof Error && error.message) return error.message

  return fallback
}

/**
 * Field-level messages from a 400 ValidationProblemDetails body, keyed by the
 * form field ids used by the trip request forms.
 */
export function getFieldErrors(error) {
  const data = error instanceof ApiError ? error.data : null
  const errors = data && typeof data === 'object' ? data.errors : null

  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return {}

  const fieldErrors = {}

  Object.entries(errors).forEach(([key, messages]) => {
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    const message = Array.isArray(messages) ? messages[0] : messages

    if (message) fieldErrors[field] = String(message)
  })

  return fieldErrors
}

export default api