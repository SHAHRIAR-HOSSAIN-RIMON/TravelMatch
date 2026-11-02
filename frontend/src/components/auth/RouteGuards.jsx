import { Navigate } from 'react-router-dom'
import { useAuth } from '../../context/useAuth.js'
import { getAuthenticatedRoute, isTokenCurrent } from '../../lib/authSessionModel.js'
import AuthSessionNotice, { InvalidSessionRecovery } from './AuthSessionNotice.jsx'

function hasValidSession(user, token) {
  return Number.isInteger(user?.id) && user.id > 0 && getAuthenticatedRoute(user?.role) && isTokenCurrent(token)
}

export function ProtectedRoute({ children, allowedRoles, redirectTo }) {
  const { isAuthenticated, isLoading, authError, user, token } = useAuth()
  if (isLoading || authError) return <AuthSessionNotice />
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (!hasValidSession(user, token)) return <InvalidSessionRecovery />
  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to={redirectTo ?? getAuthenticatedRoute(user.role)} replace />
  }
  return children
}

export function AdminRoute({ children, redirectTo }) {
  return <ProtectedRoute allowedRoles={['Admin']} redirectTo={redirectTo}>{children}</ProtectedRoute>
}

export function PublicRoute({ children }) {
  const { isAuthenticated, isLoading, authError, user, token } = useAuth()
  if (isLoading || authError) return <AuthSessionNotice />
  if (isAuthenticated) {
    if (!hasValidSession(user, token)) return <InvalidSessionRecovery />
    return <Navigate to={getAuthenticatedRoute(user.role)} replace />
  }
  return children
}
