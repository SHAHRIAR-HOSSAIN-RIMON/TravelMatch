import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import CostBreakdownCard from '../components/tourist/CostBreakdownCard.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import { formatBudget, formatDateRange, formatLongDate } from '../components/dashboard/dashboardModel.js'
import {
  formatDateLabel,
  formatSpots,
  formatTravelers,
  getOrganizedTripStatusMeta,
  getRegistrationStatusMeta,
} from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import {
  cancelRegistration,
  getAvailableOrganizedTrip,
  getMyRegistrations,
  getOrganizedTripCostEstimate,
  payForRegistration,
  registerForOrganizedTrip,
} from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

const ACTION_TEXT = {
  register: {
    title: 'Register for this trip?',
    confirmLabel: 'Register',
    description:
      'The API reserves seats against both pending and confirmed registrations, so capacity is re-checked when you register.',
  },
  pay: {
    title: 'Pay the deposit?',
    confirmLabel: 'Pay deposit',
    description:
      'This runs a simulated escrow payment. On success the API marks the registration as confirmed.',
  },
  cancel: {
    title: 'Cancel this registration?',
    confirmLabel: 'Cancel registration',
    description:
      'Cancelling releases your seats. The API does not refund deposits automatically; refunds are handled by an administrator.',
  },
}

function OrganizedTripDetailPage() {
  const { organizedTripId } = useParams()
  const routeId = Number(organizedTripId)
  const { user, token, logout } = useAuth()

  const [travelers, setTravelers] = useState(1)
  const [pendingAction, setPendingAction] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [feedback, setFeedback] = useState(null)
  const [actionError, setActionError] = useState(null)
  const [cancelReason, setCancelReason] = useState('')

  const tripLoader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) {
      throw new Error('This organized trip link is not valid.')
    }

    return getAvailableOrganizedTrip(token, routeId)
  }, [routeId, token])

  const estimateLoader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) {
      throw new Error('This trip link is not valid.')
    }

    return getOrganizedTripCostEstimate(token, routeId, travelers)
  }, [routeId, token, travelers])

  const registrationLoader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) return null

    const registrations = await getMyRegistrations(token)
    return registrations.find((item) => item.organizedTripId === routeId) ?? null
  }, [routeId, token])

  const tripResource = useResource(tripLoader, logout)
  const estimateResource = useResource(estimateLoader, logout)
  const registrationResource = useResource(registrationLoader, logout)

  const trip = tripResource.data
  const myRegistration = registrationResource.data
  const tripMeta = trip ? getOrganizedTripStatusMeta(trip.status) : null
  const registrationMeta = myRegistration ? getRegistrationStatusMeta(myRegistration.status) : null

  const remaining = Number(trip?.remainingSpots)
  const maxTravelers = Number.isFinite(remaining) && remaining > 0 ? Math.min(remaining, 100) : 100
  const deadline = trip ? formatDateLabel(trip.registrationDeadline) : null

  const canPay = myRegistration?.status === 'PendingPayment'
  const canCancel =
    myRegistration?.status === 'PendingPayment' || myRegistration?.status === 'Confirmed'

  function openAction(action) {
    setActionError(null)
    setPendingAction(action)
    if (action === 'cancel') setCancelReason('')
  }

  function closeDialog() {
    if (isSubmitting) return
    setPendingAction(null)
    setActionError(null)
  }

  function changeTravelers(delta) {
    setTravelers((current) => {
      const next = current + delta
      if (next < 1) return 1
      if (next > maxTravelers) return maxTravelers
      return next
    })
  }

  async function handleConfirm() {
    if (!pendingAction || isSubmitting) return

    setIsSubmitting(true)
    setActionError(null)

    try {
      if (pendingAction === 'register') {
        await registerForOrganizedTrip(token, routeId, { numberOfTravelers: travelers })
        setFeedback({
          tone: 'success',
          message:
            'Registration created. The API records it as Pending payment until the deposit is paid.',
        })
        registrationResource.reload()
      }

      if (pendingAction === 'pay' && myRegistration) {
        await payForRegistration(token, myRegistration.id)
        setFeedback({
          tone: 'success',
          message: 'Deposit paid and held in escrow. This registration is now confirmed.',
        })
        registrationResource.reload()
      }

      if (pendingAction === 'cancel' && myRegistration) {
        await cancelRegistration(
          token,
          myRegistration.organizedTripId,
          myRegistration.id,
          cancelReason.trim(),
        )
        setFeedback({ tone: 'success', message: 'Registration cancelled.' })
        registrationResource.reload()
      }

      setPendingAction(null)
    } catch (error) {
      setActionError(describeApiError(error, 'The action could not be completed.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const action = pendingAction ? ACTION_TEXT[pendingAction] : null

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backLabel="Organized trips"
          backTo="/organized-trips"
          description={trip?.destination?.trim() || undefined}
          eyebrow="Group trip"
          title={trip?.title?.trim() || 'Organized trip'}
        />

        {feedback ? <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner> : null}

        {tripResource.status === LOADING ? (
          <div className={styles.skeletonPanel} aria-hidden="true" />
        ) : null}

        {tripResource.status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(
              tripResource.error,
              'This organized trip could not be loaded. It may no longer be open for registration.',
            )}
            onAction={tripResource.reload}
            title="Couldn't load this trip"
            tone="error"
          >
            <Link className={styles.secondaryButton} to="/organized-trips">
              Back to organized trips
            </Link>
          </StateNotice>
        ) : null}

        {tripResource.status === READY && trip ? (
          <>
            <section className={styles.card} aria-label="Organized trip details">
              <div className={styles.cardHead}>
                <div>
                  <h2 className={styles.cardTitle}>
                    {trip.title?.trim() || `Organized trip #${trip.id}`}
                  </h2>
                  <p className={styles.cardSubtitle}>
                    {formatDateRange(trip.startDate, trip.endDate)}
                  </p>
                </div>
                {tripMeta ? <StatusPill label={tripMeta.label} tone={tripMeta.tone} /> : null}
              </div>

              <dl className={styles.metaGrid}>
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
                  <dt className={styles.metaLabel}>Maximum participants</dt>
                  <dd className={styles.metaValue}>{formatTravelers(trip.maxParticipants)}</dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Registration deadline</dt>
                  <dd className={styles.metaValue}>{deadline ?? 'Not reported'}</dd>
                </div>
              </dl>

              {trip.description?.trim() ? <p className={styles.prose}>{trip.description}</p> : null}

              {trip.inclusions?.trim() ? (
                <>
                  <h3 className={styles.sectionTitle}>Inclusions</h3>
                  <p className={styles.prose}>{trip.inclusions}</p>
                </>
              ) : null}

              {trip.exclusions?.trim() ? (
                <>
                  <h3 className={styles.sectionTitle}>Exclusions</h3>
                  <p className={styles.prose}>{trip.exclusions}</p>
                </>
              ) : null}
            </section>

            <section className={styles.panel} aria-label="Cost estimate">
              <div className={styles.panelHead}>
                <h2 className={styles.sectionTitle}>Cost estimate</h2>
                <div className={styles.stepper}>
                  <button
                    aria-label="Fewer travellers"
                    className={styles.stepperButton}
                    disabled={travelers <= 1}
                    onClick={() => changeTravelers(-1)}
                    type="button"
                  >
                    −
                  </button>
                  <span className={styles.stepperValue}>{travelers}</span>
                  <button
                    aria-label="More travellers"
                    className={styles.stepperButton}
                    disabled={travelers >= maxTravelers}
                    onClick={() => changeTravelers(1)}
                    type="button"
                  >
                    +
                  </button>
                </div>
              </div>

              <p className={styles.sectionLead}>
                The estimate is calculated by the API for the traveller count you choose. Nothing is
                charged until you register and pay the deposit.
              </p>

              {estimateResource.status === LOADING ? (
                <p className={styles.hintText}>Calculating estimate…</p>
              ) : null}

              {estimateResource.status === ERROR ? (
                <StateNotice
                  actionLabel="Retry estimate"
                  detail={describeApiError(
                    estimateResource.error,
                    'The estimate could not be calculated.',
                  )}
                  onAction={estimateResource.reload}
                  title="Estimate unavailable"
                  tone="error"
                />
              ) : null}

              {estimateResource.status === READY && estimateResource.data ? (
                <CostBreakdownCard
                  breakdown={estimateResource.data}
                  heading={`Estimate for ${formatTravelers(travelers)}`}
                />
              ) : null}
            </section>

            <section className={styles.panel} aria-label="Your registration">
              <div className={styles.panelHead}>
                <h2 className={styles.sectionTitle}>Your registration</h2>
                {registrationMeta ? (
                  <StatusPill label={registrationMeta.label} tone={registrationMeta.tone} />
                ) : null}
              </div>

              {registrationResource.status === LOADING ? (
                <p className={styles.sectionLead}>Checking your registrations…</p>
              ) : null}

              {registrationResource.status === ERROR ? (
                <StateNotice
                  actionLabel="Retry"
                  detail={describeApiError(
                    registrationResource.error,
                    'Your registrations could not be loaded.',
                  )}
                  onAction={registrationResource.reload}
                  title="Couldn't check your registration"
                  tone="error"
                />
              ) : null}

              {registrationResource.status === READY && !myRegistration ? (
                <>
                  <p className={styles.sectionLead}>
                    You are not registered for this trip yet. A cancelled registration also blocks
                    re-registering for the same trip.
                  </p>
                  <div className={styles.cardFooter}>
                    <button
                      className={styles.primaryButton}
                      disabled={Number.isFinite(remaining) && remaining <= 0}
                      onClick={() => openAction('register')}
                      type="button"
                    >
                      Register {formatTravelers(travelers)}
                    </button>
                    {Number.isFinite(remaining) && remaining <= 0 ? (
                      <span className={styles.hintText}>
                        The API reports no confirmed spots remaining.
                      </span>
                    ) : null}
                  </div>
                </>
              ) : null}

              {registrationResource.status === READY && myRegistration ? (
                <>
                  <dl className={styles.metaGrid}>
                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Travellers</dt>
                      <dd className={styles.metaValue}>
                        {formatTravelers(myRegistration.numberOfTravelers)}
                      </dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Total price</dt>
                      <dd className={styles.metaValue}>
                        {formatBudget({ budget: myRegistration.totalPrice })}
                        <span className={styles.metaNote}>No currency recorded</span>
                      </dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>Registered</dt>
                      <dd className={styles.metaValue}>
                        {formatLongDate(myRegistration.registeredAt) ?? 'Not reported'}
                      </dd>
                    </div>

                    <div className={styles.metaItem}>
                      <dt className={styles.metaLabel}>
                        {myRegistration.cancelledAt ? 'Cancelled' : 'Confirmed'}
                      </dt>
                      <dd className={styles.metaValue}>
                        {formatLongDate(
                          myRegistration.cancelledAt ?? myRegistration.confirmedAt,
                        ) ?? 'Not yet'}
                      </dd>
                    </div>
                  </dl>

                  {myRegistration.cancellationReason ? (
                    <>
                      <h3 className={styles.sectionTitle}>Cancellation reason</h3>
                      <p className={styles.prose}>{myRegistration.cancellationReason}</p>
                    </>
                  ) : null}

                  <div className={styles.cardFooter}>
                    {canPay ? (
                      <button
                        className={styles.primaryButton}
                        onClick={() => openAction('pay')}
                        type="button"
                      >
                        Pay deposit
                      </button>
                    ) : null}

                    {canCancel ? (
                      <button
                        className={styles.dangerButton}
                        onClick={() => openAction('cancel')}
                        type="button"
                      >
                        Cancel registration
                      </button>
                    ) : null}

                    {!canPay && !canCancel ? (
                      <p className={styles.hintText}>
                        This registration is closed, so no further action is available.
                      </p>
                    ) : null}
                  </div>
                </>
              ) : null}
            </section>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        busy={isSubmitting}
        cancelLabel="Close"
        confirmLabel={action?.confirmLabel ?? 'Confirm'}
        description={action?.description}
        onCancel={closeDialog}
        onConfirm={handleConfirm}
        open={Boolean(pendingAction)}
        title={action?.title ?? ''}
        tone={pendingAction === 'cancel' ? 'danger' : 'primary'}
      >
        {actionError ? <FeedbackBanner tone="error">{actionError}</FeedbackBanner> : null}

        {pendingAction === 'register' ? (
          <div className={styles.dialogField}>
            <span className={styles.hintText}>
              Registering {formatTravelers(travelers)} for {trip?.title?.trim() || 'this trip'}.
            </span>
          </div>
        ) : null}

        {pendingAction === 'cancel' ? (
          <div className={styles.dialogField}>
            <label className={styles.label} htmlFor="registration-cancel-reason">
              Reason <span className={styles.optionalMark}>(optional)</span>
            </label>
            <textarea
              className={styles.textarea}
              id="registration-cancel-reason"
              onChange={(event) => setCancelReason(event.target.value)}
              rows={3}
              value={cancelReason}
            />
          </div>
        ) : null}
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default OrganizedTripDetailPage
