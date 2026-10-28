import { useCallback, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import StatusBadge from '../components/dashboard/StatusBadge.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import {
  formatBudget,
  formatDateRange,
  formatLongDate,
  formatNights,
  sortNewestFirst,
} from '../components/dashboard/dashboardModel.js'
import { formatTravelers, tripStatusGroup } from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import { getMyTripRequests } from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

const FILTERS = [
  { id: 'all', label: 'All' },
  { id: 'open', label: 'Open' },
  { id: 'active', label: 'Active' },
  { id: 'closed', label: 'Closed' },
]

function matchesFilter(request, filterId) {
  if (filterId === 'all') return true
  return tripStatusGroup(request.status) === filterId
}

function TripCard({ request }) {
  const nights = formatNights(request.startDate, request.endDate)
  const createdOn = formatLongDate(request.createdAt)

  return (
    <article className={styles.card}>
      <div className={styles.cardHead}>
        <div>
          <h2 className={styles.cardTitle}>
            {request.destination?.trim() || `Trip request #${request.id}`}
          </h2>
          <p className={styles.cardSubtitle}>
            Request #{request.id}
            {createdOn ? `, created ${createdOn}` : ''}
          </p>
        </div>
        <StatusBadge status={request.status} />
      </div>

      <dl className={styles.metaGrid}>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Travel dates</dt>
          <dd className={styles.metaValue}>
            {formatDateRange(request.startDate, request.endDate)}
            {nights ? <span className={styles.metaNote}>{nights}</span> : null}
          </dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Travellers</dt>
          <dd className={styles.metaValue}>{formatTravelers(request.numberOfTravelers)}</dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Budget</dt>
          <dd className={styles.metaValue}>
            {formatBudget(request)}
            <span className={styles.metaNote}>No currency recorded</span>
          </dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Trip type</dt>
          <dd className={styles.metaValue}>{request.tripType?.trim() || 'Not provided'}</dd>
        </div>
      </dl>

      <div className={styles.cardFooter}>
        <Link className={styles.secondaryButton} to={`/trips/${request.id}`}>
          View details
        </Link>
      </div>
    </article>
  )
}

function MyTripsPage() {
  const { user, token, logout } = useAuth()
  const [activeFilter, setActiveFilter] = useState('all')

  const loader = useCallback(async () => sortNewestFirst(await getMyTripRequests(token)), [token])
  const { status, data, error, reload } = useResource(loader, logout)

  const requests = useMemo(() => data ?? [], [data])

  const counts = useMemo(() => {
    const base = { all: requests.length, open: 0, active: 0, closed: 0 }

    requests.forEach((request) => {
      const group = tripStatusGroup(request.status)
      if (group in base) base[group] += 1
    })

    return base
  }, [requests])

  const visible = useMemo(
    () => requests.filter((request) => matchesFilter(request, activeFilter)),
    [activeFilter, requests],
  )

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          description="Every trip request you have sent to TravelMatch guides, with the status the API currently reports."
          eyebrow="Trip requests"
          title="My trips"
        >
          <Link className={styles.primaryButton} to="/trip-requests/create">
            Create trip request
          </Link>
        </PageHeading>

        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry trip requests"
            detail={describeApiError(error, 'The trip request service could not be reached.')}
            onAction={reload}
            title="Couldn't load your trips"
            tone="error"
          />
        ) : null}

        {status === READY && requests.length === 0 ? (
          <StateNotice
            detail="Tell us where you want to go, when you want to travel and your budget. Verified local guides answer with proposals."
            title="No trip requests yet"
          >
            <Link className={styles.primaryButton} to="/trip-requests/create">
              Create your first trip request
            </Link>
          </StateNotice>
        ) : null}

        {status === LOADING ? (
          <div className={styles.grid} aria-hidden="true">
            {[0, 1, 2].map((card) => (
              <div className={styles.skeletonCard} key={card} />
            ))}
          </div>
        ) : null}

        {status === READY && requests.length > 0 ? (
          <>
            <div aria-label="Filter trips by status" className={styles.filters} role="group">
              {FILTERS.map((filter) => (
                <button
                  aria-pressed={activeFilter === filter.id}
                  className={`${styles.filter} ${activeFilter === filter.id ? styles.filterActive : ''}`}
                  key={filter.id}
                  onClick={() => setActiveFilter(filter.id)}
                  type="button"
                >
                  {filter.label}
                  <span className={styles.filterCount}>{counts[filter.id] ?? 0}</span>
                </button>
              ))}
            </div>

            {visible.length === 0 ? (
              <StateNotice
                detail="No trip requests match this filter right now."
                title="Nothing in this view"
              />
            ) : (
              <div className={styles.grid}>
                {visible.map((request) => (
                  <TripCard key={request.id} request={request} />
                ))}
              </div>
            )}
          </>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default MyTripsPage
