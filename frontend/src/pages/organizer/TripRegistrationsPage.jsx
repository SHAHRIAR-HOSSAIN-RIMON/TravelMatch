/**
 * TRAV-28 — View Registrations for My Trip
 *
 * Shows all tourist registrations for one of the organizer's trips.
 * The organizer can cancel individual registrations that are still PendingPayment
 * or Confirmed.
 *
 * Backend: GET  /api/OrganizedTrips/{id}/registrations  [Organizer]
 *          POST /api/OrganizedTrips/{id}/registrations/{regId}/cancel  [Organizer]
 */

import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../../components/dashboard/StateNotice.jsx'
import ConfirmDialog from '../../components/tourist/ConfirmDialog.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../../components/tourist/PageHeading.jsx'
import StatusPill from '../../components/tourist/StatusPill.jsx'
import { formatBudget, formatLongDate } from '../../components/dashboard/dashboardModel.js'
import { formatTravelers } from '../../components/tourist/touristModel.js'
import {
  canCancelRegistration,
  getRegistrationStatusMeta,
} from '../../lib/organizerModel.js'
import {
  cancelRegistrationAsOrganizer,
  getTripRegistrations,
} from '../../lib/organizerService.js'
import { useAuth } from '../../context/useAuth.js'
import { describeApiError } from '../../lib/api.js'
import { ERROR, LOADING, READY, useResource } from '../../lib/useResource.js'
import tStyles from '../../components/tourist/Tourist.module.css'
import oStyles from '../../components/organizer/Organizer.module.css'

// ── Registration card ─────────────────────────────────────────────────────────

function RegistrationCard({ registration, onCancelRequest }) {
  const statusMeta = getRegistrationStatusMeta(registration.status)
  const cancellable = canCancelRegistration(registration.status)
  const tourist = registration.tourist ?? {}

  return (
    <article className={oStyles.registrationCard}>
      {/* Tourist identity */}
      <div className={oStyles.touristInfo}>
        <h3 className={oStyles.touristName}>
          {tourist.fullName?.trim() || `Tourist #${tourist.id ?? registration.id}`}
        </h3>
        {tourist.email ? (
          <span className={oStyles.touristEmail}>{tourist.email}</span>
        ) : null}
        {tourist.phoneNumber ? (
          <span className={oStyles.touristPhone}>{tourist.phoneNumber}</span>
        ) : null}
      </div>

      <dl className={tStyles.metaGrid}>
        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Status</dt>
          <dd className={tStyles.metaValue}>
            <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Travellers</dt>
          <dd className={tStyles.metaValue}>
            {formatTravelers(registration.numberOfTravelers)}
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Total price</dt>
          <dd className={tStyles.metaValue}>
            {formatBudget({ budget: registration.totalPrice })}
            <span className={tStyles.metaNote}>No currency stored</span>
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Registered</dt>
          <dd className={tStyles.metaValue}>
            {formatLongDate(registration.registeredAt) ?? '—'}
          </dd>
        </div>

        {registration.confirmedAt ? (
          <div className={tStyles.metaItem}>
            <dt className={tStyles.metaLabel}>Confirmed</dt>
            <dd className={tStyles.metaValue}>
              {formatLongDate(registration.confirmedAt)}
            </dd>
          </div>
        ) : null}

        {registration.cancelledAt ? (
          <div className={tStyles.metaItem}>
            <dt className={tStyles.metaLabel}>Cancelled</dt>
            <dd className={tStyles.metaValue}>
              {formatLongDate(registration.cancelledAt)}
            </dd>
          </div>
        ) : null}
      </dl>

      {registration.cancellationReason?.trim() ? (
        <>
          <h4 className={tStyles.sectionTitle} style={{ marginTop: 14, fontSize: 13 }}>
            Cancellation reason
          </h4>
          <p className={tStyles.prose}>{registration.cancellationReason}</p>
        </>
      ) : null}

      {cancellable ? (
        <div className={tStyles.cardFooter}>
          <button
            className={tStyles.dangerButton}
            onClick={() => onCancelRequest(registration)}
            type="button"
          >
            Cancel registration
          </button>
        </div>
      ) : null}
    </article>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

function TripRegistrationsPage() {
  const { tripId } = useParams()
  const routeId = Number(tripId)
  const { user, token, logout } = useAuth()

  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const [feedback, setFeedback] = useState(null)
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [cancelError, setCancelError] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const loader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) {
      throw new Error('Invalid trip link.')
    }
    return getTripRegistrations(token, routeId)
  }, [routeId, token])

  const { status, data, error, reload } = useResource(loader, logout)
  const registrations = data ?? []
  const filtered = registrations.filter((registration) =>
    (filter === 'all' || registration.status === filter) &&
    `${registration.tourist?.fullName ?? ''} ${registration.tourist?.email ?? ''}`.toLowerCase().includes(search.trim().toLowerCase()))

  // ── Aggregate counts for the strip ──────────────────────────────────────────
  const confirmed = registrations.filter((r) => r.status === 'Confirmed').length
  const pending = registrations.filter((r) => r.status === 'PendingPayment').length
  const cancelled = registrations.filter((r) => r.status === 'Cancelled').length
  const totalTravelers = registrations
    .filter((r) => r.status === 'Confirmed')
    .reduce((sum, r) => sum + (r.numberOfTravelers ?? 0), 0)

  // ── Cancel handler ───────────────────────────────────────────────────────────

  function openCancelDialog(registration) {
    setCancelReason('')
    setCancelError(null)
    setCancelTarget(registration)
    setFeedback(null)
  }

  function closeCancelDialog() {
    if (isSubmitting) return
    setCancelTarget(null)
    setCancelError(null)
  }

  async function handleCancelConfirm() {
    if (!cancelTarget || isSubmitting) return
    setIsSubmitting(true)
    setCancelError(null)

    try {
      await cancelRegistrationAsOrganizer(
        token,
        routeId,
        cancelTarget.id,
        cancelReason.trim(),
      )
      setFeedback({
        tone: 'success',
        message: `Registration for ${cancelTarget.tourist?.fullName ?? `#${cancelTarget.id}`} cancelled.`,
      })
      setCancelTarget(null)
      reload()
    } catch (err) {
      setCancelError(describeApiError(err, 'The registration could not be cancelled.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={tStyles.container}>
        <PageHeading
          backTo={`/organizer/trips/${routeId}`}
          backLabel="Trip details"
          eyebrow="Organizer"
          title="Registered tourists"
          description="All tourist registrations for this trip. Confirmed registrations have paid their deposit."
        >
          <Link className={tStyles.secondaryButton} to={`/organized-trips/${routeId}/chat`}>Group chat</Link>
          <Link className={tStyles.secondaryButton} to="/organizer/dashboard">
            My trips
          </Link>
        </PageHeading>

        {feedback ? (
          <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner>
        ) : null}

        {/* Stats strip */}
        {status === READY && registrations.length > 0 ? (
          <div className={oStyles.statsStrip}>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{registrations.length}</span>
              <span className={oStyles.statLabel}>Total</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{confirmed}</span>
              <span className={oStyles.statLabel}>Confirmed</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{pending}</span>
              <span className={oStyles.statLabel}>Deposit due</span>
            </div>
            <div className={oStyles.statCard}>
              <span className={oStyles.statValue}>{totalTravelers}</span>
              <span className={oStyles.statLabel}>Confirmed travellers</span>
            </div>
            {cancelled > 0 ? (
              <div className={oStyles.statCard}>
                <span className={oStyles.statValue}>{cancelled}</span>
                <span className={oStyles.statLabel}>Cancelled</span>
              </div>
            ) : null}
          </div>
        ) : null}

        {/* Error */}
        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(
              error,
              'Registrations could not be loaded. Make sure you own this trip.',
            )}
            onAction={reload}
            title="Couldn't load registrations"
            tone="error"
          >
            <Link className={tStyles.secondaryButton} to="/organizer/dashboard">
              Back to my trips
            </Link>
          </StateNotice>
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
        {status === READY && registrations.length === 0 ? (
          <StateNotice
            title="No registrations yet"
            detail="When tourists register for this trip their details will appear here."
          >
            <Link className={tStyles.secondaryButton} to={`/organizer/trips/${routeId}`}>
              Back to trip details
            </Link>
          </StateNotice>
        ) : null}

        {status === READY && <section className={tStyles.panel} aria-label="Filter registrations">
          <div className={tStyles.row}>
            <label className={tStyles.field}>Search name or email<input className={tStyles.input} type="search" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
            <label className={tStyles.field}>Registration status<select className={tStyles.input} value={filter} onChange={(event) => setFilter(event.target.value)}>
              <option value="all">All registrations</option><option value="PendingPayment">Deposit due</option><option value="Confirmed">Confirmed</option><option value="Cancelled">Cancelled</option>
            </select></label>
          </div>
          <button className={tStyles.secondaryButton} type="button" onClick={reload}>Refresh registrations</button>
          <p>{filtered.length} of {registrations.length} registrations shown</p>
        </section>}
        {/* Registration cards */}
        {status === READY && registrations.length > 0 ? (
          <div className={oStyles.registrationList}>
            {filtered.map((reg) => (
              <RegistrationCard
                key={reg.id}
                onCancelRequest={openCancelDialog}
                registration={reg}
              />
            ))}
          </div>
        ) : null}
      </div>

      {/* Cancel registration dialog */}
      <ConfirmDialog
        busy={isSubmitting}
        cancelLabel="Keep registration"
        confirmLabel="Cancel registration"
        description="Cancelling releases the tourist's reserved spots. The API does not issue an automatic refund — refunds are handled by an administrator."
        onCancel={closeCancelDialog}
        onConfirm={handleCancelConfirm}
        open={Boolean(cancelTarget)}
        title={`Cancel ${cancelTarget?.tourist?.fullName ?? 'this registration'}?`}
        tone="danger"
      >
        {cancelError ? (
          <FeedbackBanner tone="error">{cancelError}</FeedbackBanner>
        ) : null}

        <div className={tStyles.dialogField}>
          <label className={tStyles.label} htmlFor="organizer-cancel-reason">
            Reason{' '}
            <span className={tStyles.optionalMark}>(optional)</span>
          </label>
          <textarea
            className={tStyles.textarea}
            id="organizer-cancel-reason"
            onChange={(e) => setCancelReason(e.target.value)}
            rows={3}
            value={cancelReason}
          />
        </div>
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default TripRegistrationsPage
