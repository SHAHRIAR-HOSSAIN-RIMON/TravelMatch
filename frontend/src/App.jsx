import OrganizedTripChatPage from './pages/OrganizedTripChatPage.jsx'
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
import PaymentHistoryPage from './pages/PaymentHistoryPage.jsx'

// Guide Module pages (Phase 3)
import GuideDashboardPage from './pages/GuideDashboardPage.jsx'
import BrowseTripRequestsPage from './pages/BrowseTripRequestsPage.jsx'
import GuideTripRequestDetailPage from './pages/GuideTripRequestDetailPage.jsx'
import GuideProposalsPage from './pages/GuideProposalsPage.jsx'
import GuideProposalDetailPage from './pages/GuideProposalDetailPage.jsx'
import GuideTripsPage from './pages/GuideTripsPage.jsx'
import GuideOrganizedTripsPage from './pages/GuideOrganizedTripsPage.jsx'
import GuideOrganizedTripDetailPage from './pages/GuideOrganizedTripDetailPage.jsx'

// Admin Module pages (Phase 7)
import AdminDashboardPage from './pages/admin/AdminDashboardPage.jsx'
import AdminUsersPage from './pages/admin/AdminUsersPage.jsx'
import AdminGuideVerificationsPage from './pages/admin/AdminGuideVerificationsPage.jsx'
import AdminDisputesPage from './pages/admin/AdminDisputesPage.jsx'

// Organizer Module pages (Phase 4)
import OrganizerDashboardPage from './pages/organizer/OrganizerDashboardPage.jsx'
import CreateOrganizedTripPage from './pages/organizer/CreateOrganizedTripPage.jsx'
import OrganizerTripDetailPage from './pages/organizer/OrganizerTripDetailPage.jsx'
import TripRegistrationsPage from './pages/organizer/TripRegistrationsPage.jsx'

import { AdminRoute, ProtectedRoute, PublicRoute } from './components/auth/RouteGuards.jsx'
import { useAuth } from './context/useAuth.js'

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
  '/payments': 'Payment History & Escrow — TravelMatch',

  // Guide pages
  '/guide/dashboard': 'Guide Dashboard — TravelMatch',
  '/guide/trip-requests': 'Browse Available Trip Requests — TravelMatch',
  '/guide/proposals': 'My Proposals — TravelMatch',
  '/guide/organized-trips': 'Organized Trips — TravelMatch',
  '/guide/trips': 'My Assigned Trips — TravelMatch',

  // Admin pages (Phase 7)
  '/admin/dashboard': 'Admin Dashboard — TravelMatch',
  '/admin/users': 'User Management — TravelMatch',
  '/admin/verifications': 'Guide Verification Queue — TravelMatch',
  '/admin/disputes': 'Dispute Management — TravelMatch',

  // Organizer pages (Phase 4)
  '/organizer/dashboard': 'My Organized Trips — TravelMatch',
  '/organizer/trips/create': 'Create Organized Trip — TravelMatch',
  '/organizer/trips': 'Organized Trip — TravelMatch',
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

function DashboardDispatcher() {
  const { user } = useAuth()
  if (user?.role === 'Admin') {
    return <Navigate replace to="/admin/dashboard" />
  }
  if (user?.role === 'Guide') {
    return <Navigate replace to="/guide/dashboard" />
  }
  if (user?.role === 'Organizer') {
    return <Navigate replace to="/organizer/dashboard" />
  }
  return <TouristDashboardPage />
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

        {/* Unified Dashboard Entry (Routes to Tourist, Guide, Admin or Organizer based on role) */}
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute allowedRoles={['Tourist', 'Guide', 'Admin', 'Organizer']}>
              <DashboardDispatcher />
            </ProtectedRoute>
          }
        />

        {/* Tourist Module Routes */}
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
          path="/guide/organized-trips"
          element={<ProtectedRoute allowedRoles={['Guide']}><GuideOrganizedTripsPage /></ProtectedRoute>}
        />
        <Route
          path="/guide/organized-trips/:tripId"
          element={<ProtectedRoute allowedRoles={['Guide']}><GuideOrganizedTripDetailPage /></ProtectedRoute>}
        />
        <Route
          path="/organized-trips/:tripId/chat"
          element={<ProtectedRoute allowedRoles={['Organizer', 'Guide', 'Tourist']}><OrganizedTripChatPage /></ProtectedRoute>}
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
        <Route
          path="/payments"
          element={
            <ProtectedRoute allowedRoles={['Tourist']}>
              <PaymentHistoryPage />
            </ProtectedRoute>
          }
        />

        {/* Guide Module Routes (Phase 3) */}
        <Route
          path="/guide/dashboard"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <GuideDashboardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/guide/trip-requests"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <BrowseTripRequestsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/guide/trip-requests/:id"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <GuideTripRequestDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/guide/proposals"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <GuideProposalsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/guide/proposals/:proposalId"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <GuideProposalDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/guide/trips"
          element={
            <ProtectedRoute allowedRoles={['Guide']}>
              <GuideTripsPage />
            </ProtectedRoute>
          }
        />

        {/* Admin Module Routes (Phase 7) */}
        <Route
          path="/admin"
          element={
            <AdminRoute>
              <Navigate replace to="/admin/dashboard" />
            </AdminRoute>
          }
        />
        <Route
          path="/admin/dashboard"
          element={
            <AdminRoute>
              <AdminDashboardPage />
            </AdminRoute>
          }
        />
        <Route
          path="/admin/users"
          element={
            <AdminRoute>
              <AdminUsersPage />
            </AdminRoute>
          }
        />
        <Route
          path="/admin/verifications"
          element={
            <AdminRoute>
              <AdminGuideVerificationsPage />
            </AdminRoute>
          }
        />
        <Route
          path="/admin/disputes"
          element={
            <AdminRoute>
              <AdminDisputesPage />
            </AdminRoute>
          }
        />
        <Route
          path="/admin/*"
          element={
            <AdminRoute>
              <Navigate replace to="/admin/dashboard" />
            </AdminRoute>
          }
        />

        {/* Organizer Module Routes (Phase 4) */}
        <Route
          path="/organizer"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <Navigate replace to="/organizer/dashboard" />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizer/dashboard"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <OrganizerDashboardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizer/trips/create"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <CreateOrganizedTripPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizer/trips/:tripId"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <OrganizerTripDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizer/trips/:tripId/registrations"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <TripRegistrationsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizer/*"
          element={
            <ProtectedRoute allowedRoles={['Organizer']}>
              <Navigate replace to="/organizer/dashboard" />
            </ProtectedRoute>
          }
        />

        <Route path="*" element={<Navigate replace to="/" />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
