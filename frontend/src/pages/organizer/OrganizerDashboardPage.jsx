import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../../components/dashboard/StateNotice.jsx'
import PageHeading from '../../components/tourist/PageHeading.jsx'
import StatusPill from '../../components/tourist/StatusPill.jsx'
import { formatDateRange, formatLongDate } from '../../components/dashboard/dashboardModel.js'
import {
  canCancelTrip,
  canEditTrip,
  canPublishTrip,
  getOrganizerTripStatusMeta,
  isTripTerminal,
} from '../../lib/organizerModel.js'
import { getMyOrganizedTrips } from '../../lib/organizerService.js'
import { useAuth } from '../../context/useAuth.js'
import { describeApiError } from '../../lib/api.js'
import { ERROR, LOADING, READY, useResource } from '../../lib/useResource.js'
import tStyles from '../../components/tourist/Tourist.module.css'
import oStyles from '../../components/organizer/Organizer.module.css'

/** Aggregate counts shown in the stats strip. */
function computeStats(trips) {
  const stats = { total: 0, draft: 0, active: 0, completed: 0, cancelled: 0 }
  trips.forEach((t) => {
    stats.total += 1
    if (t.status === 'Draft') stats.draft += 1
    else if (t.status === 'Completed') stats.completed += 1
    else if (t.status === 'Cancelled') stats.cancelled += 1
    else stats.active += 1
  })
  return stats
}

function TripCard({ trip }) {
  const statusMeta = getOrganizerTripStatusMeta(trip.status)
  const editable = canEditTrip(trip.status)
  const publishable = canPublishTrip(trip.status)
  const cancellable = canCancelTrip(trip.status)
  const terminal = isTripTerminal(trip.status)

  return (
    <article className={tStyles.card}>
      <div className={tStyles.cardHead}>
        <div>
          <h2 className={tStyles.cardTitle}>
            {trip.title?.trim() || `Trip #${trip.id}`}
          </h2>
          <p className={tStyles.cardSubtitle}>
            {trip.destination?.trim() || 'Destination not set'}
          </p>
        </div>
        <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
      </div>

      <dl className={tStyles.metaGrid}>
        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Travel dates</dt>
          <dd className={tStyles.metaValue}>
            {formatDateRange(trip.startDate, trip.endDate)}
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Max participants</dt>
          <dd className={tStyles.metaValue}>
            {trip.maxParticipants ?? '—'}
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Price per person</dt>
          <dd className={tStyles.metaValue}>
            {trip.pricePerPerson != null
              ? Number(trip.pricePerPerson).toLocaleString('en-US', { maximumFractionDigits: 2 })
              : '—'}
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Created</dt>
          <dd className={tStyles.metaValue}>
            {formatLongDate(trip.createdAt) ?? '—'}
          </dd>
        </div>
      </dl>

      <div className={tStyles.cardFooter}>
        <Link
          className={tStyles.primaryButton}
          to={`/organizer/trips/${trip.id}`}
        >
          View details
        </Link>

        {editable ? (
          <Link
            className={tStyles.secondaryButton}
            to={`/organizer/trips/${trip.id}/edit`}
          >
            Edit
          </Link>
        ) : null}

        {publishable ? (
          <Link
            className={tStyles.secondaryButton}
            to={`/organizer/trips/${trip.id}`}
            state={{ promptPublish: true }}
          >
            Publish
          </Link>
        ) : null}

        <Link
          className={tStyles.secondaryButton}
          to={`/organizer/trips/${trip.id}/registrations`}
        >
          Registrations
        </Link>

        {!cancellable ? (
          <span className={tStyles.hintText}>
            {trip.status === 'Cancelled' ? 'Already cancelled' : 'Cannot be cancelled'}
          </span>
        ) : null}
      </div>
    </article>
  )
}

function OrganizerDashboardPage() {
  const { user, token, logout } = useAuth()

  const loader = useCallback(() => getMyOrganizedTrips(token), [token])
  const { status, data, error, reload } = useResource(loader, logout)
  const trips = data ?? []
  const stats = computeStats(trips)

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={tStyles.container}>
        <PageHeading
          eyebrow="Organizer"
          title="My organized trips"
          description="Manage your group departures: create drafts, publish for guide applications, and track registrations."
        >
          <Link className={tStyles.primaryButton} to="/organizer/trips/create">
            + Create trip
          </Link>
        </PageHeading>

        {/* Stats strip — only shown once data is loaded */}
        {status === READY && trips.length > 0 ? (
          <div className={oStyles.statsStrip}>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{stats.total}</span>
              <span className={oStyles.statLabel}>Total trips</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{stats.draft}</span>
              <span className={oStyles.statLabel}>Drafts</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{stats.active}</span>
              <span className={oStyles.statLabel}>Active</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{stats.completed}</span>
              <span className={oStyles.statLabel}>Completed</span>
            </div>
          </div>
        ) : null}

        {/* Error state */}
        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(error, 'Your organized trips could not be loaded.')}
            onAction={reload}
            title="Couldn't load your trips"
            tone="error"
          />
        ) : null}

        {/* Loading skeleton */}
        {status === LOADING ? (
          <div className={tStyles.grid} aria-hidden="true">
            {[0, 1, 2].map((i) => (
              <div className={tStyles.skeletonCard} key={i} />
            ))}
          </div>
        ) : null}

        {/* Empty state */}
        {status === READY && trips.length === 0 ? (
          <StateNotice
            title="No organized trips yet"
            detail="Create your first group departure. Trips start as drafts so you can fill in all the details before publishing."
          >
            <Link className={tStyles.primaryButton} to="/organizer/trips/create">
              Create your first trip
            </Link>
          </StateNotice>
        ) : null}

        {/* Trip cards */}
        {status === READY && trips.length > 0 ? (
          <div className={tStyles.grid}>
            {trips.map((trip) => (
              <TripCard key={trip.id} trip={trip} />
            ))}
          </div>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default OrganizerDashboardPage
