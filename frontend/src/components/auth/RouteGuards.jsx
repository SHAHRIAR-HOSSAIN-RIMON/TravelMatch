import { useAuth } from '../../context/useAuth.js'
import { Navigate } from 'react-router-dom'

export function ProtectedRoute({ children, allowedRoles }) {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return null
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />
  }

  return children
}

export function PublicRoute({ children }) {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return null
  }

  if (isAuthenticated) {
    const target =
      user?.role === 'Admin'
        ? '/admin/dashboard'
        : user?.role === 'Tourist'
        ? '/dashboard'
        : user?.role === 'Guide'
        ? '/guide/dashboard'
        : '/'

    return <Navigate to={target} replace />
  }

  return children
}
