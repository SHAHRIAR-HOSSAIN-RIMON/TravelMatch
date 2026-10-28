import { useAuth } from '../../context/useAuth.js'
import { Navigate } from 'react-router-dom'

function getDefaultAuthenticatedRoute(role) {
  switch (role) {
    case 'Admin':
      return '/admin/dashboard'
    case 'Guide':
      return '/guide/dashboard'
    case 'Tourist':
      return '/dashboard'
    default:
      return '/'
  }
}

export function ProtectedRoute({ children, allowedRoles, redirectTo }) {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return null
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    const target = redirectTo ?? getDefaultAuthenticatedRoute(user.role)
    return <Navigate to={target} replace />
  }

  return children
}

export function AdminRoute({ children, redirectTo }) {
  return (
    <ProtectedRoute allowedRoles={['Admin']} redirectTo={redirectTo}>
      {children}
    </ProtectedRoute>
  )
}

export function PublicRoute({ children }) {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return null
  }

  if (isAuthenticated) {
    const target = getDefaultAuthenticatedRoute(user?.role)
    return <Navigate to={target} replace />
  }

  return children
}
