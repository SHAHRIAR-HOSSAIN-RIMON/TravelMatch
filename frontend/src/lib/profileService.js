import { api, ApiError } from './api.js'

export async function getProfile(token) {
  const response = await api.get('/api/Profile/me', { token })
  return response.data ?? response.Data
}

export async function updateProfile(token, data) {
  const response = await api.put('/api/Profile/me', data, { token })
  return response.data ?? response.Data
}

/**
 * Backend: POST /api/Profile/me/photo (multipart/form-data, field "photo").
 * Accepts JPG, PNG, WEBP or GIF up to 5 MB. Returns the updated profile.
 */
export async function uploadProfilePhoto(token, file) {
  const formData = new FormData()
  formData.append('photo', file)

  const base = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')
  const response = await fetch(`${base}/api/Profile/me/photo`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: formData,
  })

  const isJson = response.headers.get('content-type')?.includes('application/json')
  const data = isJson ? await response.json() : null

  if (!response.ok) {
    const message = data?.message || data?.title || `Upload failed (HTTP ${response.status})`
    throw new ApiError(message, response.status, data)
  }

  return data?.data ?? data?.Data
}

/** The photo URL for whichever role profile the user has. */
export function getProfilePhotoUrl(profile) {
  return (
    profile?.guideProfile?.photoUrl ||
    profile?.touristProfile?.photoUrl ||
    profile?.organizerProfile?.photoUrl ||
    ''
  )
}
