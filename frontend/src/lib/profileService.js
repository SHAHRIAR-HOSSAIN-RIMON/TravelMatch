import { api } from './api.js'

export async function getProfile(token) {
  const response = await api.get('/api/Profile/me', { token })
  return response.data ?? response.Data
}

export async function updateProfile(token, data) {
  const response = await api.put('/api/Profile/me', data, { token })
  return response.data ?? response.Data
}
