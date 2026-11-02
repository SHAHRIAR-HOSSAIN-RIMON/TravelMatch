import { getTokenExpiry, InvalidSessionError, isTokenCurrent, normalizeIdentity, normalizeLogin } from './authSessionModel.js'

const EXPIRED_MESSAGE = 'Your session expired. Please log in again.'
const RETRY_MESSAGE = 'We could not reach the server to restore your session. Your saved login has been kept. Retry when the server is available, or log in again.'

/** One session owner prevents slow startup requests from overwriting a newer login. */
export function createAuthSession({ storage, loadUser, submitCredentials, now = Date.now, schedule = setTimeout, cancel = clearTimeout }) {
  let state = { user: null, token: null, isLoading: true, isAuthenticated: false, authError: null, sessionMessage: null }
  const listeners = new Set()
  let generation = 0
  let request = null
  let expiryTimer = null
  let stopped = false

  function update(next) {
    state = { ...state, ...next }
    listeners.forEach((listener) => listener())
  }

  function invalidate() {
    generation += 1
    request?.abort()
    request = null
    cancel(expiryTimer)
    expiryTimer = null
    return generation
  }

  function clear(message = null) {
    invalidate()
    // In-memory recovery must work even if browser storage is unavailable.
    try { storage.clear() } catch { /* A restricted browser may reject storage access. */ }
    update({ user: null, token: null, isLoading: false, isAuthenticated: false, authError: null, sessionMessage: message })
  }

  function watchExpiry(token) {
    cancel(expiryTimer)
    const remaining = getTokenExpiry(token) - now()
    if (remaining <= 0) { clear(EXPIRED_MESSAGE); return }
    expiryTimer = schedule(() => {
      if (state.token !== token || stopped) return
      if (!isTokenCurrent(token, now())) checkSession()
      else watchExpiry(token)
    }, Math.min(remaining, 2_147_483_647))
  }

  async function timedRequest(operation, controller) {
    let timer
    try {
      return await Promise.race([
        operation(controller.signal),
        new Promise((_, reject) => {
          timer = schedule(() => {
            controller.abort()
            reject(new Error('The server took too long to respond. Please try again.'))
          }, 15_000)
        }),
      ])
    } finally { cancel(timer) }
  }

  async function restore() {
    if (stopped) return
    const version = invalidate()
    update({ user: null, token: null, isAuthenticated: false, isLoading: true, authError: null })
    let saved
    try {
      saved = storage.read()
      if (!saved.token) { clear(saved.invalid ? EXPIRED_MESSAGE : state.sessionMessage); return }
      if (!isTokenCurrent(saved.token, now())) { clear(EXPIRED_MESSAGE); return }
      request = new AbortController()
      const result = await timedRequest((signal) => loadUser(saved.token, { signal }), request)
      if (version !== generation || stopped) return
      const latest = storage.read()
      if (latest.token !== saved.token) {
        if (!latest.token && latest.invalid) clear(EXPIRED_MESSAGE)
        else void restore()
        return
      }
      if (!isTokenCurrent(saved.token, now())) { clear(EXPIRED_MESSAGE); return }
      const user = normalizeIdentity(result)
      update({ user, token: saved.token, isAuthenticated: true, isLoading: false, authError: null, sessionMessage: null })
      watchExpiry(saved.token)
    } catch (error) {
      if (version !== generation || stopped) return
      if (saved?.token) {
        try {
          const latest = storage.read()
          if (latest.token !== saved.token) {
            if (!latest.token && latest.invalid) clear(EXPIRED_MESSAGE)
            else void restore()
            return
          }
        } catch { /* The retry screen also handles unavailable browser storage. */ }
      }
      if (error instanceof InvalidSessionError || [401, 403, 404].includes(error.status)) {
        clear(error instanceof InvalidSessionError ? error.message : 'Your saved session is no longer valid. Please log in again.')
      } else {
        update({ isLoading: false, authError: RETRY_MESSAGE })
      }
    } finally {
      if (version === generation) request = null
    }
  }

  async function login(credentials) {
    const version = invalidate()
    request = new AbortController()
    try {
      const result = await timedRequest((signal) => submitCredentials(credentials, { signal }), request)
      if (version !== generation || stopped) throw new Error('Sign-in was interrupted. Please try again.')
      const { token, ...user } = normalizeLogin(result, now())
      storage.write(token, credentials.remember)
      update({ user, token, isAuthenticated: true, isLoading: false, authError: null, sessionMessage: null })
      watchExpiry(token)
      return result
    } catch (error) {
      if (version === generation && error instanceof InvalidSessionError) clear(error.message)
      throw error
    } finally {
      if (version === generation) request = null
    }
  }

  function checkSession() {
    if (stopped) return
    // Do not race a login/restore request with focus events.
    if (state.isLoading || request) return
    try {
      const saved = storage.read()
      if (state.token && !isTokenCurrent(state.token, now()) && !saved.token) { clear(EXPIRED_MESSAGE); return }
      if (saved.token !== state.token || state.authError) void restore()
    } catch {
      update({ authError: 'Browser storage is unavailable. Please allow site storage and retry.' })
    }
  }

  return {
    subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener) },
    getSnapshot: () => state,
    restore,
    login,
    logout: () => clear(),
    recoverInvalidSession: () => clear(state.token && !isTokenCurrent(state.token, now()) ? EXPIRED_MESSAGE : 'Your saved session is invalid. Please log in again.'),
    checkSession,
    start() { stopped = false; void restore() },
    stop() { stopped = true; invalidate() },
  }
}
