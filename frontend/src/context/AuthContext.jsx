import { useState, useEffect, useSyncExternalStore } from 'react'
import { submitLogin, getCurrentUser } from '../lib/authService.js'
import { createAuthSession } from '../lib/authSession.js'
import { createAuthStorage, TOKEN_KEY } from '../lib/authSessionModel.js'
import { AuthContext } from './AuthContext.js'

export function AuthProvider({ children }) {
  const [session] = useState(() => createAuthSession({
    storage: {
      read: () => createAuthStorage(localStorage, sessionStorage).read(),
      write: (token, persistent) => createAuthStorage(localStorage, sessionStorage).write(token, persistent),
      clear: () => createAuthStorage(localStorage, sessionStorage).clear(),
    },
    loadUser: getCurrentUser,
    submitCredentials: submitLogin,
  }))
  const state = useSyncExternalStore(session.subscribe, session.getSnapshot)

  useEffect(() => {
    session.start()
    const onVisible = () => { if (document.visibilityState === 'visible') session.checkSession() }
    const onStorage = (event) => { if (event.key === TOKEN_KEY || event.key === null) session.checkSession() }
    document.addEventListener('visibilitychange', onVisible)
    window.addEventListener('focus', onVisible)
    window.addEventListener('online', onVisible)
    window.addEventListener('storage', onStorage)
    return () => {
      session.stop()
      document.removeEventListener('visibilitychange', onVisible)
      window.removeEventListener('focus', onVisible)
      window.removeEventListener('online', onVisible)
      window.removeEventListener('storage', onStorage)
    }
  }, [session])

  const value = {
    ...state,
    login: session.login,
    logout: session.logout,
    retrySession: session.restore,
    recoverInvalidSession: session.recoverInvalidSession,
  }
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export { AuthContext }
