import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import StatusBadge from '../components/dashboard/StatusBadge.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import TripRequestEditForm from '../components/tourist/TripRequestEditForm.jsx'
import {
  formatBudget,
  formatCurrency,
  formatDateRange,
  formatLongDate,
  formatMatchingScore,
  formatNights,
  formatRating,
  MILESTONE_STATUS,
  milestoneStatusLabel,
  normalizeMilestoneStatus,
  normalizePaymentStatus,
  PAYMENT_STATUS,
  paymentStatusMeta,
} from '../components/dashboard/dashboardModel.js'
import {
  CANCEL_STEP,
  LIFECYCLE_STEPS,
  canAcceptProposal,
  formatTravelers,
  getProposalStatusMeta,
  isTripEditable,
  nextLifecycleStep,
} from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import {
  getMyPayments,
  getTripRequestLifecycle,
  getTripRequestProposals,
  payForProposal,
  releaseEscrow,
  releaseMilestone,
  transitionTripRequest,
} from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

const STEP_CONFIRMATIONS = {
  'proposals-received':
    'This records that guides have responded to your request. The API requires at least one submitted proposal.',
  matched: 'This confirms the guide you selected and locks the match for this request.',
  'payment-pending':
    'Record the price you agreed with your guide. The API stores the figure you enter against this trip.',
  planned: 'Marks the itinerary as agreed and moves the trip to Planned.',
  'in-progress': 'Marks the trip as started.',
  completed:
    'Marks the trip as completed. Releasing escrow to your guide only becomes possible afterwards.',
}

const STEP_SUCCESS = {
  'proposals-received': 'Proposals marked as received.',
  matched: 'Guide confirmed.',
  'payment-pending': 'Agreed price recorded.',
  planned: 'Trip marked as planned.',
  'in-progress': 'Trip started.',
  completed: 'Trip completed.',
  cancelled: 'Trip request cancelled.',
}

function proposalMeta(proposal) {
  const guide = proposal.guide ?? {}

  return [
    formatRating(guide.averageRating),
    guide.experienceYears > 0 ? `${guide.experienceYears} years guiding` : null,
    formatMatchingScore(proposal.matchingScore),
    guide.serviceArea ? `Serves ${guide.serviceArea}` : null,
  ]
    .filter(Boolean)
    .join(', ')
}

function findProposalPayment(payments) {
  const forProposal = payments.filter((payment) => {
    const targetType = payment.targetType ?? payment.TargetType
    const proposalId = payment.proposalId ?? payment.ProposalId
    return targetType === 0 || targetType === 'Proposal' || proposalId != null
  })

  if (forProposal.length === 0) return null

  const preferredStatuses = [
    PAYMENT_STATUS.Held,
    PAYMENT_STATUS.PartiallyRefunded,
    PAYMENT_STATUS.Released,
    PAYMENT_STATUS.Completed,
  ]
  for (const status of preferredStatuses) {
    const match = forProposal.find(
      (payment) => normalizePaymentStatus(payment.status ?? payment.Status) === status,
    )
    if (match) return match
  }

  return forProposal[0]
}

function getProposalIdFromPayment(payment) {
  return payment?.proposalId ?? payment?.ProposalId ?? null
}

function TripDetailPage() {
  const { tripId } = useParams()
  const routeId = Number(tripId)
  const { user, token, logout } = useAuth()

  const [isEditing, setIsEditing] = useState(false)
  const [feedback, setFeedback] = useState(null)
  const [pendingStep, setPendingStep] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [actionError, setActionError] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [agreedPrice, setAgreedPrice] = useState('')
  const [chosenProposalId, setChosenProposalId] = useState('')

  // Payment-specific state
  const [isPaying, setIsPaying] = useState(false)
  const [paymentFeedback, setPaymentFeedback] = useState(null)
  const [paymentError, setPaymentError] = useState(null)
  const [sessionPayment, setSessionPayment] = useState(null)

  // Escrow release state
  const [isReleasing, setIsReleasing] = useState(false)
  const [releaseFeedback, setReleaseFeedback] = useState(null)
  const [releaseError, setReleaseError] = useState(null)
  const [pendingRelease, setPendingRelease] = useState(null) // { paymentId, milestoneId? }

  const loader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) {
      throw new Error('This trip request link is not valid.')
    }

    const [lifecycle, proposals, payments] = await Promise.all([
      getTripRequestLifecycle(token, routeId),
      getTripRequestProposals(token, routeId),
      getMyPayments(token),
    ])

    return { lifecycle, proposals, payments }
  }, [routeId, token])

  const { status, data, error, reload } = useResource(loader, logout)

  const [stateRouteId, setStateRouteId] = useState(routeId)
  if (stateRouteId !== routeId) {
    setStateRouteId(routeId)
    setSessionPayment(null)
    setPaymentFeedback(null)
    setPaymentError(null)
  }

  const lifecycle = data?.lifecycle ?? null
  const proposals = data?.proposals ?? []
  const payments = data?.payments ?? []
  const allowedTransitions = lifecycle?.allowedTransitions ?? []
  const tripStatus = lifecycle?.status
  const editable = isTripEditable(tripStatus)
  const canCancel = allowedTransitions.includes(CANCEL_STEP.key)
  const nextStep = nextLifecycleStep(allowedTransitions)

  const proposalPayment = findProposalPayment(payments)
  let acceptedProposalId = getProposalIdFromPayment(proposalPayment)
  if (!acceptedProposalId) {
    try {
      acceptedProposalId = localStorage.getItem(`travelmatch_accepted_proposal_${routeId}`)
    } catch {
      // Ignore localStorage errors
    }
  }
  const paymentData = sessionPayment ?? proposalPayment
  const paymentStatus = normalizePaymentStatus(paymentData?.status ?? paymentData?.Status)
  const isPaymentHeld =
    paymentStatus === PAYMENT_STATUS.Held || paymentStatus === PAYMENT_STATUS.PartiallyRefunded
  const isPaymentFailed = paymentStatus === PAYMENT_STATUS.Failed
  const canPay = tripStatus === 'Matched' || tripStatus === 'PaymentPending'
  const canRetryPayment = isPaymentFailed && canPay
  const depositedAt = paymentData?.completedAt ?? paymentData?.CompletedAt ?? null
  const releasedAt = paymentData?.escrowReleasedAt ?? paymentData?.EscrowReleasedAt ?? null

  const title = lifecycle?.destination?.trim() || 'Trip request'
  const nights = lifecycle ? formatNights(lifecycle.startDate, lifecycle.endDate) : null
  const createdOn = lifecycle ? formatLongDate(lifecycle.createdAt) : null

  function openStep(step) {
    setActionError(null)
    setPendingStep(step)

    if (step.key === 'cancelled') {
      setCancelReason('')
    }

    if (step.key === 'payment-pending') {
      setAgreedPrice(lifecycle?.acceptedPrice != null ? String(lifecycle.acceptedPrice) : '')
    }

    if (step.key === 'matched') {
      setChosenProposalId(proposals[0] ? String(proposals[0].id) : '')
    }
  }

  function closeDialog() {
    if (isSubmitting) return
    setPendingStep(null)
    setActionError(null)
  }

  async function confirmStep() {
    if (!pendingStep || isSubmitting) return

    const payload = {}

    if (pendingStep.key === 'matched') {
      const proposalId = Number(chosenProposalId)
      if (!Number.isInteger(proposalId) || proposalId <= 0) {
        setActionError('Choose a proposal first.')
        return
      }
      payload.proposalId = proposalId
    }

    if (pendingStep.key === 'payment-pending') {
      const agreedAmount = Number(agreedPrice)
      if (!Number.isFinite(agreedAmount) || agreedAmount <= 0) {
        setActionError('Enter the price agreed with your guide.')
        return
      }
      payload.acceptedPrice = agreedAmount
    }

    if (pendingStep.key === 'cancelled') {
      payload.cancellationReason = cancelReason.trim() || null
    }

    setIsSubmitting(true)
    setActionError(null)

    try {
      await transitionTripRequest(token, routeId, pendingStep.key, payload)
      setFeedback({ tone: 'success', message: STEP_SUCCESS[pendingStep.key] ?? 'Trip updated.' })
      setPendingStep(null)
      reload()
    } catch (submitError) {
      setActionError(describeApiError(submitError, 'The trip status could not be changed.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handlePay(proposalId) {
    if (isPaying) return

    setIsPaying(true)
    setPaymentError(null)
    setPaymentFeedback(null)

    try {
      const result = await payForProposal(token, proposalId, {})
      setSessionPayment(result)
      setPaymentFeedback({
        tone: 'success',
        message: 'Payment deposited and held in escrow. You can now mark the trip as planned.',
      })
      reload()
    } catch (error) {
      const errorMessage = describeApiError(error, 'The payment could not be processed.')
      setPaymentError(errorMessage)
      setPaymentFeedback({
        tone: 'error',
        message: errorMessage,
      })
    } finally {
      setIsPaying(false)
    }
  }

  // ── Escrow release helpers ────────────────────────────────────────────────

  function isPaymentHeldOrPartiallyRefunded(payment) {
    const status = normalizePaymentStatus(payment?.status ?? payment?.Status)
    return status === PAYMENT_STATUS.Held || status === PAYMENT_STATUS.PartiallyRefunded
  }

  function isTripCompleted(lifecycle) {
    return lifecycle?.status === 'Completed'
  }

  function canReleaseEscrow(payment, lifecycle) {
    return isPaymentHeldOrPartiallyRefunded(payment) && isTripCompleted(lifecycle)
  }

  function getMilestones(payment) {
    return payment?.milestones ?? payment?.Milestones ?? []
  }

  function getHeldMilestones(milestones) {
    return milestones.filter(
      (milestone) =>
        normalizeMilestoneStatus(milestone.status ?? milestone.Status) === MILESTONE_STATUS.Held,
    )
  }

  function allMilestonesReleased(milestones) {
    return milestones.length > 0 && getHeldMilestones(milestones).length === 0
  }

  // ── Escrow release handlers ────────────────────────────────────────────────

  function openRelease(paymentId, milestoneId = null) {
    setReleaseError(null)
    setReleaseFeedback(null)
    setPendingRelease({ paymentId, milestoneId })
  }

  function closeReleaseDialog() {
    if (isReleasing) return
    setPendingRelease(null)
    setReleaseError(null)
  }

  async function confirmRelease() {
    if (!pendingRelease || isReleasing) return

    const { paymentId, milestoneId } = pendingRelease
    setIsReleasing(true)
    setReleaseError(null)
    setReleaseFeedback(null)

    try {
      let result
      if (milestoneId) {
        result = await releaseMilestone(token, paymentId, milestoneId)
      } else {
        result = await releaseEscrow(token, paymentId)
      }

      setSessionPayment(result)
      const message = milestoneId
        ? `Milestone released successfully.`
        : 'Escrow released successfully. Funds have been transferred to the guide.'
      setReleaseFeedback({ tone: 'success', message })
      setPendingRelease(null)
      reload()
    } catch (error) {
      const errorMessage = describeApiError(error, milestoneId ? 'The milestone could not be released.' : 'The escrow could not be released.')
      setReleaseError(errorMessage)
      setReleaseFeedback({ tone: 'error', message: errorMessage })
    } finally {
      setIsReleasing(false)
    }
  }

  function handleSaved() {
    setIsEditing(false)
    setFeedback({ tone: 'success', message: 'Trip request updated.' })
    reload()
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backLabel="My trips"
          backTo="/trips"
          description={
            createdOn
              ? `Request #${lifecycle?.id ?? routeId}, created ${createdOn}.`
              : `Request #${routeId}.`
          }
          eyebrow="Trip request"
          title={title}
        />

        {feedback ? <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner> : null}

        {status === LOADING ? <div className={styles.skeletonPanel} aria-hidden="true" /> : null}

        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(error, 'This trip request could not be loaded.')}
            onAction={reload}
            title="Couldn't load this trip"
            tone="error"
          >
            <Link className={styles.secondaryButton} to="/trips">
              Back to my trips
            </Link>
          </StateNotice>
        ) : null}

        {status === READY && lifecycle ? (
          <>
            <section className={styles.card} aria-label="Trip request details">
              <div className={styles.cardHead}>
                <div>
                  <h2 className={styles.cardTitle}>{title}</h2>
                  <p className={styles.cardSubtitle}>
                    {formatDateRange(lifecycle.startDate, lifecycle.endDate)}
                    {nights ? `, ${nights}` : ''}
                  </p>
                </div>
                <StatusBadge status={lifecycle.status} />
              </div>

              <dl className={styles.metaGrid}>
                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Travellers</dt>
                  <dd className={styles.metaValue}>
                    {formatTravelers(lifecycle.numberOfTravelers)}
                  </dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Budget</dt>
                  <dd className={styles.metaValue}>
                    {formatBudget(lifecycle)}
                    <span className={styles.metaNote}>No currency recorded</span>
                  </dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Trip type</dt>
                  <dd className={styles.metaValue}>{lifecycle.tripType?.trim() || 'Not provided'}</dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Agreed price</dt>
                  <dd className={styles.metaValue}>
                    {lifecycle.acceptedPrice != null
                      ? formatBudget({ budget: lifecycle.acceptedPrice })
                      : 'Not recorded yet'}
                    {lifecycle.acceptedPrice != null ? (
                      <span className={styles.metaNote}>No currency recorded</span>
                    ) : null}
                  </dd>
                </div>
              </dl>

              {lifecycle.description ? (
                <>
                  <h3 className={styles.sectionTitle}>Description</h3>
                  <p className={styles.prose}>{lifecycle.description}</p>
                </>
              ) : null}

              {lifecycle.travelPreferences ? (
                <>
                  <h3 className={styles.sectionTitle}>Travel preferences</h3>
                  <p className={styles.prose}>{lifecycle.travelPreferences}</p>
                </>
              ) : null}

              {lifecycle.cancellationReason ? (
                <>
                  <h3 className={styles.sectionTitle}>Cancellation reason</h3>
                  <p className={styles.prose}>{lifecycle.cancellationReason}</p>
                </>
              ) : null}

              <div className={styles.cardFooter}>
                {editable ? (
                  <button
                    className={styles.secondaryButton}
                    onClick={() => {
                      setFeedback(null)
                      setIsEditing(true)
                    }}
                    type="button"
                  >
                    Edit request
                  </button>
                ) : (
                  <p className={styles.hintText}>
                    Editing is closed for this request: the API only accepts changes while it is
                    Pending.
                  </p>
                )}
              </div>
            </section>

            {isEditing && editable ? (
              <TripRequestEditForm
                onCancelEdit={() => setIsEditing(false)}
                onSaved={handleSaved}
                request={lifecycle}
                token={token}
              />
            ) : null}

            {/* Payment status section - shown when a proposal has been accepted */}
            {acceptedProposalId != null && (
              <section className={styles.panel} aria-label="Payment status">
                <div className={styles.panelHead}>
                  <h2 className={styles.sectionTitle}>Payment</h2>
                </div>

                <p className={styles.sectionLead}>
                  The accepted proposal requires a deposit to place funds in simulated escrow before the trip can be marked as planned.
                </p>

                {paymentData ? (
                  <>
                    {paymentFeedback ? (
                      <FeedbackBanner tone={paymentFeedback.tone}>{paymentFeedback.message}</FeedbackBanner>
                    ) : null}

                    <div className={styles.breakdownRows}>
                      <div className={styles.breakdownRow}>
                        <dt className={styles.breakdownLabel}>Payment status</dt>
                        <dd className={styles.breakdownValue}>
                          <StatusPill {...paymentStatusMeta(paymentData)} />
                        </dd>
                      </div>
                      <div className={styles.breakdownRow}>
                        <dt className={styles.breakdownLabel}>Amount</dt>
                        <dd className={styles.breakdownValue}>
                          {formatCurrency(
                            paymentData?.amount ?? paymentData?.Amount,
                            paymentData?.currency ?? paymentData?.Currency ?? 'USD',
                          )}
                        </dd>
                      </div>
                      <div className={styles.breakdownRow}>
                        <dt className={styles.breakdownLabel}>Transaction reference</dt>
                        <dd className={styles.breakdownValue}>
                          {paymentData?.transactionReference ?? paymentData?.TransactionReference ?? '—'}
                        </dd>
                      </div>
                      <div className={styles.breakdownRow}>
                        <dt className={styles.breakdownLabel}>Deposited</dt>
                        <dd className={styles.breakdownValue}>
                          {depositedAt ? new Date(depositedAt).toLocaleString() : '—'}
                        </dd>
                      </div>
                      {releasedAt ? (
                        <div className={styles.breakdownRow}>
                          <dt className={styles.breakdownLabel}>Released</dt>
                          <dd className={styles.breakdownValue}>
                            {new Date(releasedAt).toLocaleString()}
                          </dd>
                        </div>
                      ) : null}
                      {getMilestones(paymentData).length > 0 && (
                        <div className={styles.breakdownNote}>
                          <strong>Milestones:</strong>
                          <ul className={styles.milestoneList}>
                            {getMilestones(paymentData).map((milestone) => {
                              const milestoneStatus = normalizeMilestoneStatus(
                                milestone.status ?? milestone.Status,
                              )
                              const isReleased = milestoneStatus === MILESTONE_STATUS.Released
                              const isHeld = milestoneStatus === MILESTONE_STATUS.Held
                              return (
                                <li key={milestone.id ?? milestone.MilestoneId} className={styles.milestoneItem}>
                                  <span className={styles.milestoneLabel}>
                                    {milestone.label ?? milestone.Label}: {formatCurrency(milestone.amount ?? milestone.Amount, milestone.currency ?? paymentData?.currency ?? paymentData?.Currency ?? 'USD')}
                                  </span>
                                  <span className={`${styles.milestoneStatus} ${isReleased ? styles.milestoneReleased : isHeld ? styles.milestoneHeld : ''}`}>
                                    {milestoneStatusLabel(milestone.status ?? milestone.Status)}
                                  </span>
                                  {isHeld && canReleaseEscrow(paymentData, lifecycle) && (
                                    <button
                                      className={styles.milestoneReleaseButton}
                                      onClick={() => openRelease(paymentData.paymentId ?? paymentData.PaymentId, milestone.id ?? milestone.MilestoneId)}
                                      disabled={isReleasing}
                                      type="button"
                                    >
                                      Release
                                    </button>
                                  )}
                                </li>
                              )
                            })}
                          </ul>
                        </div>
                      )}
                      {isPaymentHeld && (
                        <p className={styles.breakdownNote}>
                          {canReleaseEscrow(paymentData, lifecycle)
                            ? 'The trip is completed. You can release the escrow to the guide.'
                            : 'Funds are held in simulated escrow. They will be released to the guide after the trip is completed.'}
                        </p>
                      )}
                    </div>

                    <div className={styles.cardFooter}>
                      {isPaymentHeld && canReleaseEscrow(paymentData, lifecycle) && (
                        getMilestones(paymentData).length === 0 ? (
                          <button
                            className={styles.primaryButton}
                            onClick={() => openRelease(paymentData.paymentId ?? paymentData.PaymentId)}
                            disabled={isReleasing}
                            type="button"
                          >
                            {isReleasing ? (
                              <>
                                <span className={styles.spinner} aria-hidden="true" />
                                Releasing…
                              </>
                            ) : (
                              'Release escrow'
                            )}
                          </button>
                        ) : (
                          <>
                            <p className={styles.hintText}>
                              All milestones must be released individually. The payment will be marked as released once all milestones are settled.
                            </p>
                            {getHeldMilestones(getMilestones(paymentData)).length === 0 && allMilestonesReleased(getMilestones(paymentData)) && (
                              <StatusPill label="All milestones released — escrow settled" tone="closed" />
                            )}
                          </>
                        )
                      )}

                      {isPaymentFailed && canPay && acceptedProposalId != null && (
                        <button
                          className={styles.primaryButton}
                          onClick={() => handlePay(acceptedProposalId)}
                          disabled={isPaying}
                          type="button"
                        >
                          {isPaying ? (
                            <>
                              <span className={styles.spinner} aria-hidden="true" />
                              Processing…
                            </>
                          ) : (
                            'Retry payment'
                          )}
                        </button>
                      )}

                      {acceptedProposalId != null && (
                        <Link
                          className={styles.secondaryButton}
                          to={`/trips/${routeId}/proposals/${acceptedProposalId}`}
                        >
                          View proposal
                        </Link>
                      )}
                    </div>

                    {releaseFeedback && (
                      <FeedbackBanner tone={releaseFeedback.tone}>{releaseFeedback.message}</FeedbackBanner>
                    )}

                    {releaseError && !isReleasing && (
                      <p className={styles.hintText} style={{ color: 'var(--tm-danger-600)' }}>
                        {releaseError}
                      </p>
                    )}

                    {paymentError && !isPaying && (
                      <p className={styles.hintText} style={{ color: 'var(--tm-danger-600)' }}>
                        {paymentError}
                      </p>
                    )}
                  </>
                ) : canPay && acceptedProposalId != null ? (
                  <>
                    {paymentFeedback ? (
                      <FeedbackBanner tone={paymentFeedback.tone}>{paymentFeedback.message}</FeedbackBanner>
                    ) : null}

                    <div className={styles.cardFooter}>
                      <button
                        className={styles.primaryButton}
                        onClick={() => handlePay(acceptedProposalId)}
                        disabled={isPaying}
                        type="button"
                      >
                        {isPaying ? (
                          <>
                            <span className={styles.spinner} aria-hidden="true" />
                            Processing…
                          </>
                        ) : (
                          'Pay deposit'
                        )}
                      </button>

                      {canRetryPayment && !isPaying && (
                        <button
                          className={styles.secondaryButton}
                          onClick={() => handlePay(acceptedProposalId)}
                          type="button"
                        >
                          Retry payment
                        </button>
                      )}

                      <Link
                        className={styles.secondaryButton}
                        to={`/trips/${routeId}/proposals/${acceptedProposalId}`}
                      >
                        View proposal
                      </Link>
                    </div>

                    {paymentError && !isPaying && (
                      <p className={styles.hintText} style={{ color: 'var(--tm-danger-600)' }}>
                        {paymentError}
                      </p>
                    )}
                  </>
                ) : canPay ? (
                  <>
                    {paymentFeedback ? (
                      <FeedbackBanner tone={paymentFeedback.tone}>{paymentFeedback.message}</FeedbackBanner>
                    ) : null}

                    <div className={styles.cardFooter}>
                      <p className={styles.hintText}>
                        A deposit is required to proceed, but the proposal reference could not be determined.
                        Please navigate to the proposal from your proposals list to make the payment.
                      </p>
                      <Link
                        className={styles.secondaryButton}
                        to="/proposals"
                      >
                        Go to proposals
                      </Link>
                    </div>

                    {paymentError && !isPaying && (
                      <p className={styles.hintText} style={{ color: 'var(--tm-danger-600)' }}>
                        {paymentError}
                      </p>
                    )}
                  </>
                ) : (
                  <>
                    <p className={styles.hintText}>
                      No deposit was recorded for this trip.
                    </p>
                    <div className={styles.cardFooter}>
                      {acceptedProposalId != null && (
                        <Link
                          className={styles.secondaryButton}
                          to={`/trips/${routeId}/proposals/${acceptedProposalId}`}
                        >
                          View proposal
                        </Link>
                      )}
                    </div>
                  </>
                )}
              </section>
            )}

            <section className={styles.panel} aria-label="Trip progress">
              <div className={styles.panelHead}>
                <h2 className={styles.sectionTitle}>Trip progress</h2>
                <span className={styles.sectionCount}>
                  {allowedTransitions.length === 0
                    ? 'No further transitions'
                    : `${allowedTransitions.length} available`}
                </span>
              </div>
              <p className={styles.sectionLead}>
                Only the transitions the API reports as legal right now can be applied. Steps that
                are not legal stay disabled.
              </p>

              <div className={styles.stepGrid}>
                {LIFECYCLE_STEPS.map((step) => {
                  const allowed = allowedTransitions.includes(step.key)
                  const isNext = nextStep?.key === step.key

                  return (
                    <button
                      className={`${styles.stepButton} ${isNext ? styles.stepButtonNext : ''}`}
                      disabled={!allowed}
                      key={step.key}
                      onClick={() => openStep(step)}
                      type="button"
                    >
                      <span className={styles.stepLabel}>{step.label}</span>
                      <span className={styles.stepHint}>{step.hint}</span>
                    </button>
                  )
                })}
              </div>
            </section>

            <section className={styles.panel} aria-label="Proposals received">
              <div className={styles.panelHead}>
                <h2 className={styles.sectionTitle}>Proposals received</h2>
                <span className={styles.sectionCount}>{proposals.length}</span>
              </div>
              <p className={styles.sectionLead}>
                The API lists submitted proposals only. Once you accept one, the others are marked
                as not selected.
              </p>

              {proposals.length === 0 ? (
                <p className={styles.prose}>
                  No submitted proposals yet. Guides who respond will appear here.
                </p>
              ) : (
                <ul className={styles.list}>
                  {proposals.map((proposal) => {
                    const guide = proposal.guide ?? {}
                    const meta = proposalMeta(proposal)
                    const proposalMetaStatus = getProposalStatusMeta(proposal.status)
                    const acceptable = canAcceptProposal(tripStatus, proposal.status)

                    return (
                      <li className={styles.listRow} key={proposal.id}>
                        <div className={styles.listRowMain}>
                          <p className={styles.listRowTitle}>
                            {guide.name?.trim() || `Guide #${guide.id ?? proposal.id}`}
                            {guide.isVerified ? (
                              <span className={styles.verifiedTag}>Verified guide</span>
                            ) : null}
                            <StatusPill
                              label={proposalMetaStatus.label}
                              tone={proposalMetaStatus.tone}
                            />
                          </p>
                          {meta ? <p className={styles.listRowMeta}>{meta}</p> : null}
                          {proposal.availability?.trim() ? (
                            <p className={styles.listRowMeta}>
                              Availability: {proposal.availability}
                            </p>
                          ) : null}
                        </div>

                        <div className={styles.listRowAside}>
                          <span className={styles.listRowAmountLabel}>Proposed price</span>
                          <span className={styles.listRowAmount}>
                            {formatBudget({ budget: proposal.price })}
                          </span>
                          <Link
                            className={styles.secondaryButton}
                            to={`/trips/${routeId}/proposals/${proposal.id}`}
                          >
                            {acceptable ? 'Review and accept' : 'View proposal'}
                          </Link>
                        </div>
                      </li>
                    )
                  })}
                </ul>
              )}
            </section>

            <div className={styles.dangerZone}>
              <div className={styles.dangerText}>
                <h2 className={styles.dangerTitle}>Cancel this trip request</h2>
                <p className={styles.dangerLead}>
                  {canCancel
                    ? 'Cancelling is final for this request. The API does not reopen a cancelled request.'
                    : 'Cancelling is not available at this stage of the lifecycle.'}
                </p>
              </div>

              <button
                className={styles.dangerButton}
                disabled={!canCancel}
                onClick={() => openStep(CANCEL_STEP)}
                type="button"
              >
                Cancel trip request
              </button>
            </div>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        busy={isSubmitting}
        cancelLabel="Keep as is"
        confirmLabel={pendingStep?.key === 'cancelled' ? 'Cancel trip request' : 'Confirm'}
        description={
          pendingStep
            ? STEP_CONFIRMATIONS[pendingStep.key] ??
              'This cannot be undone from the app once applied.'
            : undefined
        }
        onCancel={closeDialog}
        onConfirm={confirmStep}
        open={Boolean(pendingStep)}
        title={pendingStep?.label ?? ''}
        tone={pendingStep?.key === 'cancelled' ? 'danger' : 'primary'}
      >
        {actionError ? <FeedbackBanner tone="error">{actionError}</FeedbackBanner> : null}

        {pendingStep?.key === 'matched' ? (
          <div className={styles.dialogField}>
            <label className={styles.label} htmlFor="confirm-proposal">
              Proposal to confirm
            </label>
            <select
              className={styles.select}
              id="confirm-proposal"
              onChange={(event) => setChosenProposalId(event.target.value)}
              value={chosenProposalId}
            >
              {proposals.map((proposal) => (
                <option key={proposal.id} value={proposal.id}>
                  {proposal.guide?.name?.trim() || `Proposal #${proposal.id}`}
                </option>
              ))}
            </select>
          </div>
        ) : null}

        {pendingStep?.key === 'payment-pending' ? (
          <div className={styles.dialogField}>
            <label className={styles.label} htmlFor="confirm-price">
              Price agreed with your guide
            </label>
            <input
              className={styles.input}
              id="confirm-price"
              min="0"
              onChange={(event) => setAgreedPrice(event.target.value)}
              step="any"
              type="number"
              value={agreedPrice}
            />
            <span className={styles.hintText}>
              The API stores the amount you enter here as the agreed price.
            </span>
          </div>
        ) : null}

        {pendingStep?.key === 'cancelled' ? (
          <div className={styles.dialogField}>
            <label className={styles.label} htmlFor="confirm-cancellation-reason">
              Reason <span className={styles.optionalMark}>(optional)</span>
            </label>
            <textarea
              className={styles.textarea}
              id="confirm-cancellation-reason"
              onChange={(event) => setCancelReason(event.target.value)}
              rows={3}
              value={cancelReason}
            />
          </div>
        ) : null}
      </ConfirmDialog>

      {/* Escrow release confirmation dialog */}
      <ConfirmDialog
        busy={isReleasing}
        cancelLabel="Keep in escrow"
        confirmLabel={pendingRelease?.milestoneId ? 'Release milestone' : 'Release escrow'}
        description={
          pendingRelease
            ? pendingRelease.milestoneId
              ? 'This releases a single milestone to the guide. This action cannot be undone.'
              : 'This releases the full escrow amount to the guide. The trip must be completed and no open disputes may exist. This action cannot be undone.'
            : undefined
        }
        onCancel={closeReleaseDialog}
        onConfirm={confirmRelease}
        open={Boolean(pendingRelease)}
        title={pendingRelease?.milestoneId ? 'Release milestone' : 'Release escrow'}
        tone="primary"
      >
        {releaseError ? <FeedbackBanner tone="error">{releaseError}</FeedbackBanner> : null}
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default TripDetailPage
