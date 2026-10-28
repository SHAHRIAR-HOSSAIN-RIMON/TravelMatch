import { useState, useEffect, useCallback } from 'react'
import { submitLogin, getCurrentUser } from '../lib/authService.js'
import { AuthContext } from './AuthContext.js'

const TOKEN_KEY = 'travelmatch_token'

function getStoredToken() {
  const local = localStorage.getItem(TOKEN_KEY)
  if (local) return { token: local, persistent: true }
  const session = sessionStorage.getItem(TOKEN_KEY)
  if (session) return { token: session, persistent: false }
  return null
}

function setStoredToken(token, persistent) {
  if (persistent) {
    localStorage.setItem(TOKEN_KEY, token)
    sessionStorage.removeItem(TOKEN_KEY)
  } else {
    sessionStorage.setItem(TOKEN_KEY, token)
    localStorage.removeItem(TOKEN_KEY)
  }
}

function clearStoredToken() {
  localStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(TOKEN_KEY)
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [token, setToken] = useState(null)
  const [isLoading, setIsLoading] = useState(true)

  const isAuthenticated = Boolean(user && token)

  const initializeAuth = useCallback(async () => {
    const stored = getStoredToken()
    if (!stored) {
      setIsLoading(false)
      return
    }

    try {
      const userData = await getCurrentUser(stored.token)
      if (userData) {
        setUser(userData)
        setToken(stored.token)
      } else {
        clearStoredToken()
      }
    } catch {
      clearStoredToken()
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    let mounted = true

    async function init() {
      await initializeAuth()
      if (!mounted) return
    }

    init()

    return () => {
      mounted = false
    }
  }, [initializeAuth])

  const login = useCallback(async ({ email, password, remember }) => {
    const result = await submitLogin({ email, password })

    const newToken = result.token
    const userData = {
      id: result.id,
      fullName: result.fullName,
      email: result.email,
      role: result.role,
    }

    setStoredToken(newToken, remember)
    setToken(newToken)
    setUser(userData)

    return result
  }, [])

  const logout = useCallback(() => {
    clearStoredToken()
    setToken(null)
    setUser(null)
  }, [])

  const value = {
    user,
    token,
    isAuthenticated,
    isLoading,
    login,
    logout,
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}

export { AuthContext }