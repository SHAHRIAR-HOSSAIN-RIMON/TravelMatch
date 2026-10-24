import { api } from './api.js'

const ROLE_MAP = {
  tourist: 0,
  'local-guide': 1,
  'tour-organizer': 2,
}

function normalizeResponse(response) {
  if (!response || typeof response !== 'object') return response
  return {
    success: response.success ?? response.Success,
    data: response.data ?? response.Data,
    message: response.message ?? response.Message,
    error: response.error ?? response.Error,
  }
}

export async function submitLogin({ email, password }) {
  const response = await api.post('/api/Auth/login', { email, password })
  return normalizeResponse(response)
}

export async function submitSignup({ fullName, email, phoneNumber, password, confirmPassword, role }) {
  const payload = {
    fullName,
    email,
    phoneNumber,
    password,
    confirmPassword,
    role: ROLE_MAP[role] ?? 0,
  }
  const response = await api.post('/api/Auth/register', payload)
  return normalizeResponse(response)
}

export async function getCurrentUser(token) {
  const response = await api.get('/api/Auth/me', { token })
  return normalizeResponse(response)
}