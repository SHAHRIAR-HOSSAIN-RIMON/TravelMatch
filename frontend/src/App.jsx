import { useEffect } from 'react'
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import LandingPage from './components/landing/LandingPage.jsx'
import LoginPage from './pages/LoginPage.jsx'
import SignupPage from './pages/SignupPage.jsx'
import ProfilePage from './pages/ProfilePage.jsx'
import TouristDashboardPage from './pages/TouristDashboardPage.jsx'
import CreateTripRequestPage from './pages/CreateTripRequestPage.jsx'
import MyTripsPage from './pages/MyTripsPage.jsx'
import TripDetailPage from './pages/TripDetailPage.jsx'
import ProposalsPage from './pages/ProposalsPage.jsx'
import ProposalDetailPage from './pages/ProposalDetailPage.jsx'
import OrganizedTripsPage from './pages/OrganizedTripsPage.jsx'
import OrganizedTripDetailPage from './pages/OrganizedTripDetailPage.jsx'
import RegistrationsPage from './pages/RegistrationsPage.jsx'
import { ProtectedRoute, PublicRoute } from './components/auth/RouteGuards.jsx'

const pageTitles = {
  '/': 'TravelMatch — Travel freely. Explore locally.',
  '/login': 'Log In — TravelMatch',
  '/signup': 'Create Account — TravelMatch',
  '/profile': 'Your Profile — TravelMatch',
  '/dashboard': 'Dashboard — TravelMatch',
  '/trip-requests/create': 'Create Trip Request — TravelMatch',
  '/trips': 'My Trips — TravelMatch',
  '/proposals': 'Proposals — TravelMatch',
  '/organized-trips': 'Organized Trips — TravelMatch',
  '/registrations': 'My Registrations — TravelMatch',
}

function resolveTitle(pathname) {
  if (pageTitles[pathname]) return pageTitles[pathname]

  const nestedMatch = Object.keys(pageTitles)
    .filter((path) => path !== '/' && pathname.startsWith(`${path}/`))
    .sort((left, right) => right.length - left.length)[0]

  return nestedMatch ? pageTitles[nestedMatch] : pageTitles['/']
}

function ScrollToTop() {
  const { pathname } = useLocation()

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' })
    document.title = resolveTitle(pathname)
  }, [pathname])

  return null
}

function App() {
  return (
    <BrowserRouter>
      <ScrollToTop />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route
          path="/login"
          element={
            <PublicRoute>
              <LoginPage />
            </PublicRoute>
          }
        />
        <Route
          path="/signup"
          element={
            <PublicRoute>
              <SignupPage />
            </PublicRoute>
          }
        />
        <Route
          path="/profile"
          element={
            <ProtectedRoute>
              <ProfilePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <TouristDashboardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/trip-requests/create"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <CreateTripRequestPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/trips"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <MyTripsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/trips/:tripId"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <TripDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/trips/:tripId/proposals/:proposalId"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <ProposalDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/proposals"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <ProposalsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organized-trips"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <OrganizedTripsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organized-trips/:organizedTripId"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <OrganizedTripDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/registrations"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <RegistrationsPage />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate replace to="/" />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
