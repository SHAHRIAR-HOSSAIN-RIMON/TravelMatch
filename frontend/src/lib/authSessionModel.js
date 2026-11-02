export const AUTH_ROUTES = Object.freeze({
  Tourist: '/dashboard',
  Guide: '/guide/dashboard',
  Organizer: '/organizer/dashboard',
  Admin: '/admin/dashboard',
})

export class InvalidSessionError extends Error {
  constructor(message = 'Your saved session is invalid. Please log in again.') {
    super(message)
    this.name = 'InvalidSessionError'
  }
}

export function getAuthenticatedRoute(role) {
  return typeof role === 'string' && Object.hasOwn(AUTH_ROUTES, role) ? AUTH_ROUTES[role] : null
}

export function normalizeIdentity(response) {
  const identity = response?.data ?? response?.Data ?? response
  if (!identity || typeof identity !== 'object' || Array.isArray(identity)) throw new InvalidSessionError()
  const id = identity.id ?? identity.Id
  const role = identity.role ?? identity.Role
  if (!Number.isInteger(id) || id <= 0 || !getAuthenticatedRoute(role)) throw new InvalidSessionError()
  return {
    id,
    role,
    fullName: identity.fullName ?? identity.FullName ?? '',
    email: identity.email ?? identity.Email ?? '',
  }
}

// Decoding only checks local expiry. The backend still verifies JWT signatures.
export function getTokenExpiry(token) {
  if (typeof token !== 'string' || !/^[\w-]+\.[\w-]+\.[\w-]+$/.test(token)) return null
  try {
    const encoded = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    const payload = JSON.parse(atob(encoded.padEnd(Math.ceil(encoded.length / 4) * 4, '=')))
    return typeof payload.exp === 'number' && Number.isFinite(payload.exp) && payload.exp > 0 ? payload.exp * 1000 : null
  } catch {
    return null
  }
}

export function isTokenCurrent(token, now = Date.now()) {
  const expires = getTokenExpiry(token)
  return expires !== null && expires > now
}

export function normalizeLogin(response, now = Date.now()) {
  const identity = response?.data ?? response?.Data ?? response
  const user = normalizeIdentity(identity)
  const token = identity.token ?? identity.Token
  if (!isTokenCurrent(token, now)) throw new InvalidSessionError('The server returned an invalid or expired session. Please try logging in again.')
  return { ...user, token }
}

export const TOKEN_KEY = 'travelmatch_token'

export function createAuthStorage(local, session, now = Date.now) {
  return {
    read() {
      let invalid = false
      // Remove invalid old tokens so they cannot shadow a valid session token.
      for (const storage of [local, session]) {
        const token = storage.getItem(TOKEN_KEY)
        if (!token) continue
        if (isTokenCurrent(token, now())) return { token, invalid }
        storage.removeItem(TOKEN_KEY)
        invalid = true
      }
      return { token: null, invalid }
    },
    write(token, persistent) {
      const destination = persistent ? local : session
      const other = persistent ? session : local
      destination.setItem(TOKEN_KEY, token)
      other.removeItem(TOKEN_KEY)
    },
    clear() {
      local.removeItem(TOKEN_KEY)
      session.removeItem(TOKEN_KEY)
    },
  }
}
