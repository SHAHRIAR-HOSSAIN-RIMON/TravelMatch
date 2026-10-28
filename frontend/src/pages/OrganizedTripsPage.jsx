import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import { formatBudget, formatDateRange } from '../components/dashboard/dashboardModel.js'
import {
  formatDateLabel,
  formatSpots,
  getOrganizedTripStatusMeta,
} from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import { getAvailableOrganizedTrips } from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function OrganizedTripsPage() {
  const { user, token, logout } = useAuth()

  const loader = useCallback(async () => getAvailableOrganizedTrips(token), [token])
  const { status, data, error, reload } = useResource(loader, logout)
  const trips = data ?? []

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          description="Group departures arranged by TravelMatch organizers. The API lists only trips that are open for registration, still before their deadline and with spots remaining."
          eyebrow="Group trips"
          title="Organized trips"
        >
          <Link className={styles.secondaryButton} to="/registrations">
            My registrations
          </Link>
        </PageHeading>

        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry trips"
            detail={describeApiError(error, 'Organized trips could not be loaded.')}
            onAction={reload}
            title="Couldn't load organized trips"
            tone="error"
          />
        ) : null}

        {status === LOADING ? (
          <div className={styles.grid} aria-hidden="true">
            {[0, 1, 2].map((card) => (
              <div className={styles.skeletonCard} key={card} />
            ))}
          </div>
        ) : null}

        {status === READY && trips.length === 0 ? (
          <StateNotice
            detail="Nothing is open for registration right now. Trips appear here once an organizer opens registration and while spots remain."
            title="No trips open for registration"
          >
            <Link className={styles.secondaryButton} to="/registrations">
              Check your registrations
            </Link>
          </StateNotice>
        ) : null}

        {status === READY && trips.length > 0 ? (
          <div className={styles.grid}>
            {trips.map((trip) => {
              const statusMeta = getOrganizedTripStatusMeta(trip.status)
              const deadline = formatDateLabel(trip.registrationDeadline)

              return (
                <article className={styles.card} key={trip.id}>
                  <div className={styles.cardHead}>
                    <div>
                      <h2 className={styles.cardTitle}>
                        {trip.title?.trim() || `Organized trip #${trip.id}`}
                      </h2>
                      <p className={styles.cardSubtitle}>
                        {trip.destination?.trim() || 'Destination not reported'}
                      </p>
                    </div>
                    <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
                  </div>

                  <dl className={styles.metaGrid}>
                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Travel dates</dt>
                      <dd className={styles.metaValue}>
                        {formatDateRange(trip.startDate, trip.endDate)}
                      </dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Price per person</dt>
                      <dd className={styles.metaValue}>
                        {formatBudget({ budget: trip.pricePerPerson })}
                        <span className={styles.metaNote}>No currency recorded</span>
                      </dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Availability</dt>
                      <dd className={styles.metaValue}>{formatSpots(trip.remainingSpots)}</dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Registration deadline</dt>
                      <dd className={styles.metaValue}>{deadline ?? 'Not reported'}</dd>
                    </div>
                  </dl>

                  {trip.description?.trim() ? (
                    <p className={styles.prose}>{trip.description}</p>
                  ) : null}

                  <div className={styles.cardFooter}>
                    <Link className={styles.primaryButton} to={`/organized-trips/${trip.id}`}>
                      View trip and register
                    </Link>
                  </div>
                </article>
              )
            })}
          </div>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default OrganizedTripsPage
