import test from 'node:test'
import assert from 'node:assert/strict'
import { createAuthSession } from './authSession.js'
import { createAuthStorage, getAuthenticatedRoute, getTokenExpiry, InvalidSessionError, normalizeIdentity, normalizeLogin, TOKEN_KEY } from './authSessionModel.js'

const user = { id: 5, fullName: 'Demo Tourist', email: 'demo@example.test', role: 'Tourist' }
const tokenAt = (expires) => `eyJhbGciOiJIUzI1NiJ9.${btoa(JSON.stringify({ exp: expires / 1000 })).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')}.signature`
const memoryStorage = () => {
  const values = new Map()
  return { getItem: (key) => values.get(key) ?? null, setItem: (key, value) => values.set(key, String(value)), removeItem: (key) => values.delete(key) }
}
function harness(overrides = {}) {
  let time = 1_000_000
  let nextTimer = 0
  const timers = new Map()
  const local = memoryStorage()
  const temporary = memoryStorage()
  const token = tokenAt(time + 60_000)
  const storage = createAuthStorage(local, temporary, () => time)
  const session = createAuthSession({
    storage,
    now: () => time,
    schedule(callback, delay) { const id = ++nextTimer; timers.set(id, { callback, due: time + delay }); return id },
    cancel: (id) => timers.delete(id),
    loadUser: async () => user,
    submitCredentials: async () => ({ ...user, token }),
    ...overrides,
  })
  return {
    session, storage, local, temporary, token,
    advance(ms) {
      time += ms
      for (const [id, timer] of [...timers]) if (timer.due <= time) { timers.delete(id); timer.callback() }
    },
  }
}
const flush = async () => { for (let i = 0; i < 10; i++) await Promise.resolve() }

test('malformed or HTML identity responses and unknown roles are rejected', () => {
  for (const value of ['<html>Vite fallback</html>', null, [], {}, { ...user, role: 'tourist' }, { ...user, role: ['Tourist'] }, { ...user, role: 0 }, { ...user, id: 0 }]) {
    assert.throws(() => normalizeIdentity(value), InvalidSessionError)
  }
  assert.deepEqual(normalizeIdentity({ data: user }), user)
  assert.equal(getAuthenticatedRoute(undefined), null)
  assert.equal(getAuthenticatedRoute('Tourist'), '/dashboard')
  assert.equal(getAuthenticatedRoute('Guide'), '/guide/dashboard')
  assert.equal(getAuthenticatedRoute('Organizer'), '/organizer/dashboard')
  assert.equal(getAuthenticatedRoute('Admin'), '/admin/dashboard')
})

test('JWT validation rejects expired, malformed, and missing-expiry login tokens', () => {
  for (const token of ['undefined', 'null', '', 'bad.jwt.value', 'a.e30.c', tokenAt(999_000)]) {
    assert.throws(() => normalizeLogin({ ...user, token }, 1_000_000), InvalidSessionError)
  }
  assert.equal(getTokenExpiry(tokenAt(1_060_000)), 1_060_000)
})

test('expired local storage cannot shadow a valid session-storage login', async () => {
  const h = harness()
  h.local.setItem(TOKEN_KEY, tokenAt(999_000))
  h.temporary.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  assert.equal(h.session.getSnapshot().isAuthenticated, true)
  assert.equal(h.local.getItem(TOKEN_KEY), null)
  assert.equal(h.session.getSnapshot().token, h.token)
})

test('expired saved sessions recover to login without contacting the backend', async () => {
  let calls = 0
  const h = harness({ loadUser: async () => { calls++; return user } })
  h.local.setItem(TOKEN_KEY, tokenAt(999_000))
  await h.session.restore()
  assert.equal(calls, 0)
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
  assert.match(h.session.getSnapshot().sessionMessage, /expired/)
  assert.equal(h.local.getItem(TOKEN_KEY), null)
})

test('invalid successful /me responses clear the session instead of redirecting to landing', async () => {
  const h = harness({ loadUser: async () => '<html>unexpected response</html>' })
  h.local.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
  assert.equal(h.local.getItem(TOKEN_KEY), null)
  assert.match(h.session.getSnapshot().sessionMessage, /invalid/)
})

test('temporary backend outages keep the token and retry restores the session', async () => {
  let unavailable = true
  const h = harness({ loadUser: async () => { if (unavailable) throw new TypeError('Failed to fetch'); return user } })
  h.local.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  assert.equal(h.local.getItem(TOKEN_KEY), h.token)
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
  assert.ok(h.session.getSnapshot().authError)
  unavailable = false
  await h.session.restore()
  assert.equal(h.session.getSnapshot().isAuthenticated, true)
  assert.equal(h.session.getSnapshot().authError, null)
})

test('authorization failures discard invalid saved credentials', async () => {
  for (const status of [401, 403, 404]) {
    const h = harness({ loadUser: async () => { throw Object.assign(new Error('Rejected'), { status }) } })
    h.local.setItem(TOKEN_KEY, h.token)
    await h.session.restore()
    assert.equal(h.local.getItem(TOKEN_KEY), null)
    assert.equal(h.session.getSnapshot().authError, null)
    assert.equal(h.session.getSnapshot().isAuthenticated, false)
  }
})

test('expiry while a tab stays open logs out with an explanation', async () => {
  const h = harness()
  h.local.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  h.advance(60_000)
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
  assert.equal(h.local.getItem(TOKEN_KEY), null)
  assert.match(h.session.getSnapshot().sessionMessage, /expired/)
})

test('a slow startup response cannot overwrite a newer login', async () => {
  let finishRestore
  const h = harness({ loadUser: () => new Promise((resolve) => { finishRestore = resolve }) })
  h.local.setItem(TOKEN_KEY, h.token)
  const restoring = h.session.restore()
  await h.session.login({ remember: false })
  finishRestore({ ...user, id: 99, role: 'Guide' })
  await restoring
  assert.equal(h.session.getSnapshot().user.id, user.id)
  assert.equal(h.session.getSnapshot().user.role, 'Tourist')
  assert.equal(h.local.getItem(TOKEN_KEY), null)
  assert.equal(h.temporary.getItem(TOKEN_KEY), h.token)
})

test('logout prevents a pending restore from resurrecting the session', async () => {
  let complete
  let signal
  const h = harness({ loadUser: (_, options) => { signal = options.signal; return new Promise((resolve) => { complete = resolve }) } })
  h.local.setItem(TOKEN_KEY, h.token)
  const pending = h.session.restore()
  h.session.logout()
  complete(user)
  await pending
  assert.equal(signal.aborted, true)
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
})

test('hung restoration times out with retry while preserving saved credentials', async () => {
  let signal
  const h = harness({ loadUser: (_, options) => { signal = options.signal; return new Promise(() => {}) } })
  h.local.setItem(TOKEN_KEY, h.token)
  const pending = h.session.restore()
  h.advance(15_000)
  await pending
  assert.equal(signal.aborted, true)
  assert.equal(h.session.getSnapshot().isLoading, false)
  assert.ok(h.session.getSnapshot().authError)
  assert.equal(h.local.getItem(TOKEN_KEY), h.token)
})

test('returning to a tab observes logout from another tab', async () => {
  const h = harness()
  h.local.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  h.local.removeItem(TOKEN_KEY)
  h.session.checkSession()
  await flush()
  assert.equal(h.session.getSnapshot().isAuthenticated, false)
})

test('stopping and restarting initialization, as StrictMode does, ignores the old response', async () => {
  const pending = []
  const h = harness({ loadUser: () => new Promise((resolve) => pending.push(resolve)) })
  h.local.setItem(TOKEN_KEY, h.token)
  h.session.start()
  h.session.stop()
  h.session.start()
  pending[1](user)
  await flush()
  pending[0]({ ...user, role: 'Guide' })
  await flush()
  assert.equal(h.session.getSnapshot().user.role, 'Tourist')
  h.session.stop()
})

test('StrictMode startup preserves the expired-session explanation after clearing storage', async () => {
  const h = harness()
  h.local.setItem(TOKEN_KEY, tokenAt(999_000))
  h.session.start()
  h.session.stop()
  h.session.start()
  await flush()
  assert.match(h.session.getSnapshot().sessionMessage, /expired/)
  assert.equal(h.session.getSnapshot().isLoading, false)
  h.session.stop()
})

test('an older session expiry does not clear a newer login saved by another tab', async () => {
  const h = harness()
  h.local.setItem(TOKEN_KEY, h.token)
  await h.session.restore()
  const newer = tokenAt(1_120_000)
  h.local.setItem(TOKEN_KEY, newer)
  h.advance(60_000)
  await flush()
  assert.equal(h.local.getItem(TOKEN_KEY), newer)
  assert.equal(h.session.getSnapshot().token, newer)
  assert.equal(h.session.getSnapshot().isAuthenticated, true)
})

test('an old failed restore does not erase a newer login in shared storage', async () => {
  let rejectOld
  let calls = 0
  const h = harness({ loadUser: () => { calls++; return calls === 1 ? new Promise((_, reject) => { rejectOld = reject }) : Promise.resolve(user) } })
  h.local.setItem(TOKEN_KEY, h.token)
  const pending = h.session.restore()
  const newer = tokenAt(1_120_000)
  h.local.setItem(TOKEN_KEY, newer)
  rejectOld(Object.assign(new Error('Expired previous session'), { status: 401 }))
  await pending
  await flush()
  assert.equal(h.local.getItem(TOKEN_KEY), newer)
  assert.equal(h.session.getSnapshot().token, newer)
})
