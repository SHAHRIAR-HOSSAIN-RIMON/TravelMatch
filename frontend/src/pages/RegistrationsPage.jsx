import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import CostBreakdownCard from '../components/tourist/CostBreakdownCard.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import {
  formatBudget,
  formatDateRange,
  formatLongDate,
} from '../components/dashboard/dashboardModel.js'
import { formatTravelers, getRegistrationStatusMeta } from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import {
  cancelRegistration,
  getMyRegistrations,
} from '../lib/touristService.js'
import {
  payForRegistration,
  getRegistrationCostBreakdown,
  describePaymentError,
} from '../lib/paymentService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function RegistrationCard({ registration, token, onCancelRequest, onPay }) {
  const [showBreakdown, setShowBreakdown] = useState(false)
  const [breakdown, setBreakdown] = useState(null)
  const [breakdownError, setBreakdownError] = useState(null)
  const [isLoadingBreakdown, setIsLoadingBreakdown] = useState(false)

  const statusMeta = getRegistrationStatusMeta(registration.status)
  const trip = registration.organizedTrip ?? {}
  const canPay = registration.status === 'PendingPayment'
  const canCancel =
    registration.status === 'PendingPayment' || registration.status === 'Confirmed'

  async function toggleBreakdown() {
    if (showBreakdown) {
      setShowBreakdown(false)
      return
    }

    setShowBreakdown(true)
    if (breakdown || isLoadingBreakdown) return

    setIsLoadingBreakdown(true)
    setBreakdownError(null)

    try {
      setBreakdown(await getRegistrationCostBreakdown(token, registration.id))
    } catch (error) {
      setBreakdownError(describeApiError(error, 'The cost breakdown could not be loaded.'))
    } finally {
      setIsLoadingBreakdown(false)
    }
  }

  return (
    <article className={styles.card}>
      <div className={styles.cardHead}>
        <div>
          <h2 className={styles.cardTitle}>
            {trip.title?.trim() || `Registration #${registration.id}`}
          </h2>
          <p className={styles.cardSubtitle}>
            {trip.destination?.trim() || 'Destination not reported'}
            {trip.startDate ? `, ${formatDateRange(trip.startDate, trip.endDate)}` : ''}
          </p>
        </div>
        <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
      </div>

      <dl className={styles.metaGrid}>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Travellers</dt>
          <dd className={styles.metaValue}>{formatTravelers(registration.numberOfTravelers)}</dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Total price</dt>
          <dd className={styles.metaValue}>
            {formatBudget({ budget: registration.totalPrice })}
            <span className={styles.metaNote}>No currency recorded</span>
          </dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Registered</dt>
          <dd className={styles.metaValue}>
            {formatLongDate(registration.registeredAt) ?? 'Not reported'}
          </dd>
        </div>

        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>
            {registration.cancelledAt ? 'Cancelled' : 'Confirmed'}
          </dt>
          <dd className={styles.metaValue}>
            {registration.cancelledAt
              ? formatLongDate(registration.cancelledAt)
              : formatLongDate(registration.confirmedAt) ?? 'Awaiting deposit'}
          </dd>
        </div>
      </dl>

      {registration.cancellationReason ? (
        <p className={styles.prose}>
          <strong>Cancellation reason:</strong> {registration.cancellationReason}
        </p>
      ) : null}

      {showBreakdown ? (
        <div className={styles.cardFooter}>
          {isLoadingBreakdown ? (
            <p className={styles.hintText}>Loading cost breakdown…</p>
          ) : null}

          {breakdownError ? <FeedbackBanner tone="error">{breakdownError}</FeedbackBanner> : null}

          {breakdown ? (
            <CostBreakdownCard breakdown={breakdown} heading="Registration costs" />
          ) : null}
        </div>
      ) : null}

      <div className={styles.cardFooter}>
        <button className={styles.quietButton} onClick={toggleBreakdown} type="button">
          {showBreakdown ? 'Hide cost breakdown' : 'Show cost breakdown'}
        </button>

        {canPay ? (
          <button
            className={styles.primaryButton}
            onClick={() => onPay(registration)}
            type="button"
          >
            Pay deposit into escrow
          </button>
        ) : null}

        {registration.status === 'Confirmed' ? (
          <Link className={styles.quietButton} to="/payments">
            View in Payment History
          </Link>
        ) : null}

        {canCancel ? (
          <button
            className={styles.dangerButton}
            onClick={() => onCancelRequest(registration)}
            type="button"
          >
            Cancel registration
          </button>
        ) : null}
      </div>
    </article>
  )
}

function RegistrationsPage() {
  const { user, token, logout } = useAuth()
  const [feedback, setFeedback] = useState(null)
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [cancelError, setCancelError] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const loader = useCallback(async () => getMyRegistrations(token), [token])
  const { status, data, error, reload } = useResource(loader, logout)
  const registrations = data ?? []

  const sortedRegistrations = [...registrations].sort((left, right) => {
    const leftTime = Date.parse(left?.registeredAt ?? '')
    const rightTime = Date.parse(right?.registeredAt ?? '')
    if (Number.isFinite(leftTime) && Number.isFinite(rightTime) && leftTime !== rightTime) {
      return rightTime - leftTime
    }
    return Number(right?.id ?? 0) - Number(left?.id ?? 0)
  })

  async function handlePay(registration) {
    if (isSubmitting) return

    setIsSubmitting(true)
    setFeedback(null)

    try {
      await payForRegistration(token, registration.id)
      setFeedback({ tone: 'success', message: 'Deposit paid and registration confirmed.' })
      reload()
    } catch (payError) {
      setFeedback({
        tone: 'error',
        message: describePaymentError(payError, 'The deposit could not be paid.'),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleCancel() {
    if (!cancelTarget || isSubmitting) return

    setIsSubmitting(true)
    setCancelError(null)

    try {
      await cancelRegistration(
        token,
        cancelTarget.organizedTripId,
        cancelTarget.id,
        cancelReason.trim(),
      )
      setFeedback({ tone: 'success', message: 'Registration cancelled.' })
      setCancelTarget(null)
      reload()
    } catch (cancelFailure) {
      setCancelError(describeApiError(cancelFailure, 'The registration could not be cancelled.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          description="Every organized trip you have registered for, with the status the API reports and the actions still available."
          eyebrow="Group trips"
          title="My registrations"
        >
          <Link className={styles.secondaryButton} to="/organized-trips">
            Browse organized trips
          </Link>
          <Link className={styles.secondaryButton} to="/payments">
            Payment History & Escrow
          </Link>
        </PageHeading>

        {feedback ? <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner> : null}

        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(error, 'Your registrations could not be loaded.')}
            onAction={reload}
            title="Couldn't load your registrations"
            tone="error"
          />
        ) : null}

        {status === LOADING ? (
          <div className={styles.grid} aria-hidden="true">
            {[0, 1].map((card) => (
              <div className={styles.skeletonCard} key={card} />
            ))}
          </div>
        ) : null}

        {status === READY && registrations.length === 0 ? (
          <StateNotice
            detail="Registrations you make for organized trips appear here with their status and any remaining payment steps."
            title="No registrations yet"
          >
            <Link className={styles.primaryButton} to="/organized-trips">
              Browse organized trips
            </Link>
          </StateNotice>
        ) : null}

        {status === READY && registrations.length > 0 ? (
          <div className={styles.grid}>
            {sortedRegistrations.map((registration) => (
              <RegistrationCard
                key={registration.id}
                onCancelRequest={(target) => {
                  setCancelReason('')
                  setCancelError(null)
                  setCancelTarget(target)
                }}
                onPay={handlePay}
                registration={registration}
                token={token}
              />
            ))}
          </div>
        ) : null}
      </div>

      <ConfirmDialog
        busy={isSubmitting}
        cancelLabel="Keep registration"
        confirmLabel="Cancel registration"
        description="Cancelling releases your seats. The API does not refund deposits automatically; refunds are handled by an administrator."
        onCancel={() => {
          if (isSubmitting) return
          setCancelTarget(null)
          setCancelError(null)
        }}
        onConfirm={handleCancel}
        open={Boolean(cancelTarget)}
        title="Cancel this registration?"
        tone="danger"
      >
        {cancelError ? <FeedbackBanner tone="error">{cancelError}</FeedbackBanner> : null}

        <div className={styles.dialogField}>
          <label className={styles.label} htmlFor="cancel-registration-reason">
            Reason <span className={styles.optionalMark}>(optional)</span>
          </label>
          <textarea
            className={styles.textarea}
            id="cancel-registration-reason"
            onChange={(event) => setCancelReason(event.target.value)}
            rows={3}
            value={cancelReason}
          />
        </div>
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default RegistrationsPage
