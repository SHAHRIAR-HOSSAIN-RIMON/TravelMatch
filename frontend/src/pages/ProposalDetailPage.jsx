import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import CostBreakdownCard from '../components/tourist/CostBreakdownCard.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import {
  formatBudget,
  formatCurrency,
  formatLongDate,
  formatMatchingScore,
  formatRating,
  milestoneStatusLabel,
  normalizePaymentStatus,
  PAYMENT_STATUS,
  paymentStatusMeta,
} from '../components/dashboard/dashboardModel.js'
import { getProposalStatusMeta, normalizeItinerary } from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import {
  acceptProposal,
  getMyPayments,
  getProposalCostBreakdown,
  getProposalDetail,
  payForProposal,
} from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function getMilestones(payment) {
  return payment?.milestones ?? payment?.Milestones ?? []
}

function ProposalDetailPage() {
  const { tripId, proposalId } = useParams()
  const tripRouteId = Number(tripId)
  const proposalRouteId = Number(proposalId)
  const { user, token, logout } = useAuth()

  const [isConfirming, setIsConfirming] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [feedback, setFeedback] = useState(null)
  const [actionError, setActionError] = useState(null)

  // Payment-specific state
  const [isPaying, setIsPaying] = useState(false)
  const [paymentFeedback, setPaymentFeedback] = useState(null)
  const [paymentError, setPaymentError] = useState(null)
  const [sessionPayment, setSessionPayment] = useState(null)

  const detailLoader = useCallback(async () => {
    if (!Number.isInteger(tripRouteId) || !Number.isInteger(proposalRouteId)) {
      throw new Error('This proposal link is not valid.')
    }

    return getProposalDetail(token, tripRouteId, proposalRouteId)
  }, [proposalRouteId, token, tripRouteId])

  const breakdownLoader = useCallback(async () => {
    if (!Number.isInteger(proposalRouteId)) {
      throw new Error('This proposal link is not valid.')
    }

    return getProposalCostBreakdown(token, proposalRouteId)
  }, [proposalRouteId, token])

  const paymentsLoader = useCallback(async () => {
    if (!Number.isInteger(proposalRouteId)) {
      throw new Error('This proposal link is not valid.')
    }

    return getMyPayments(token)
  }, [proposalRouteId, token])

  const detailResource = useResource(detailLoader, logout)
  const breakdownResource = useResource(breakdownLoader, logout)
  const paymentsResource = useResource(paymentsLoader, logout)

  const [stateProposalId, setStateProposalId] = useState(proposalRouteId)
  if (stateProposalId !== proposalRouteId) {
    setStateProposalId(proposalRouteId)
    setSessionPayment(null)
    setPaymentFeedback(null)
    setPaymentError(null)
  }

  const proposal = detailResource.data
  const guide = proposal?.guide ?? {}
  const itinerary = normalizeItinerary(proposal?.itinerary)
  const statusMeta = proposal ? getProposalStatusMeta(proposal.status) : null
  const submittedOn = proposal ? formatLongDate(proposal.createdAt) : null

  const persistedPayment = (paymentsResource.data ?? []).find(
    (payment) => Number(payment.proposalId ?? payment.ProposalId) === proposalRouteId,
  )
  const paymentData = sessionPayment ?? persistedPayment ?? null
  const paymentStatus = normalizePaymentStatus(paymentData?.status ?? paymentData?.Status)
  const isPaymentHeld =
    paymentStatus === PAYMENT_STATUS.Held || paymentStatus === PAYMENT_STATUS.PartiallyRefunded
  const isPaymentFailed = paymentStatus === PAYMENT_STATUS.Failed
  const canRetryPayment = isPaymentFailed
  const depositedAt = paymentData?.completedAt ?? paymentData?.CompletedAt ?? null
  const releasedAt = paymentData?.escrowReleasedAt ?? paymentData?.EscrowReleasedAt ?? null

  const guideMeta = [
    formatRating(guide.averageRating),
    guide.experienceYears > 0 ? `${guide.experienceYears} years guiding` : null,
    guide.serviceArea ? `Serves ${guide.serviceArea}` : null,
    proposal ? formatMatchingScore(proposal.matchingScore) : null,
  ]
    .filter(Boolean)
    .join(', ')

  async function handleAccept() {
    if (isSubmitting) return

    setIsSubmitting(true)
    setActionError(null)

    try {
      const result = await acceptProposal(token, tripRouteId, proposalRouteId)
      if (result?.acceptedProposalId) {
        try {
          localStorage.setItem(`travelmatch_accepted_proposal_${tripRouteId}`, String(result.acceptedProposalId))
        } catch {
          // Ignore localStorage errors
        }
      }
      setIsConfirming(false)
      setFeedback({
        tone: 'success',
        message:
          'Proposal accepted. The trip is now matched with this guide, and the other submitted proposals were declined by the API.',
      })
      detailResource.reload()
      breakdownResource.reload()
    } catch (error) {
      setActionError(describeApiError(error, 'The proposal could not be accepted.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handlePay() {
    if (isPaying) return

    setIsPaying(true)
    setPaymentError(null)
    setPaymentFeedback(null)

    try {
      const result = await payForProposal(token, proposalRouteId, {})
      setSessionPayment(result)
      setPaymentFeedback({
        tone: 'success',
        message: 'Payment deposited and held in escrow. You can now mark the trip as planned.',
      })
      detailResource.reload()
      breakdownResource.reload()
      paymentsResource.reload()
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

  const isProposalAccepted = proposal?.status === 'Accepted'
  const showPaymentSection = isProposalAccepted

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backLabel="Back to trip"
          backTo={`/trips/${Number.isInteger(tripRouteId) ? tripRouteId : ''}`}
          description={
            submittedOn ? `Proposal #${proposalRouteId}, submitted ${submittedOn}.` : undefined
          }
          eyebrow="Proposal"
          title={guide.name?.trim() || 'Guide proposal'}
        />

        {feedback ? <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner> : null}

        {detailResource.status === LOADING ? (
          <div className={styles.skeletonPanel} aria-hidden="true" />
        ) : null}

        {detailResource.status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(detailResource.error, 'This proposal could not be loaded.')}
            onAction={detailResource.reload}
            title="Couldn't load this proposal"
            tone="error"
          >
            <Link className={styles.secondaryButton} to="/proposals">
              Back to proposals
            </Link>
          </StateNotice>
        ) : null}

        {detailResource.status === READY && proposal ? (
          <>
            {proposal.isVerificationWarning ? (
              <FeedbackBanner tone="info">
                This guide is not verified yet. TravelMatch recommends confirming credentials
                before paying a deposit.
              </FeedbackBanner>
            ) : null}

            <section className={styles.card} aria-label="Proposal summary">
              <div className={styles.cardHead}>
                <div>
                  <h2 className={styles.cardTitle}>
                    {guide.name?.trim() || `Guide #${guide.id ?? proposal.id}`}
                    {guide.isVerified ? (
                      <span className={styles.verifiedTag}> Verified guide</span>
                    ) : null}
                  </h2>
                  {guideMeta ? <p className={styles.cardSubtitle}>{guideMeta}</p> : null}
                </div>
                <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
              </div>

              <dl className={styles.metaGrid}>
                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Proposed price</dt>
                  <dd className={styles.metaValue}>
                    {formatBudget({ budget: proposal.price })}
                    <span className={styles.metaNote}>No currency recorded on the proposal</span>
                  </dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Availability</dt>
                  <dd className={styles.metaValue}>
                    {proposal.availability?.trim() || 'Not provided'}
                  </dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Verification</dt>
                  <dd className={styles.metaValue}>
                    {guide.verificationStatus?.trim() || 'Not reported'}
                  </dd>
                </div>

                <div className={styles.metaItem}>
                  <dt className={styles.metaLabel}>Acceptance</dt>
                  <dd className={styles.metaValue}>
                    {proposal.isEligibleForAcceptance ? 'Open' : 'Not available now'}
                  </dd>
                </div>
              </dl>

              {proposal.inclusions?.trim() ? (
                <>
                  <h3 className={styles.sectionTitle}>Inclusions</h3>
                  <p className={styles.prose}>{proposal.inclusions}</p>
                </>
              ) : null}

              {proposal.exclusions?.trim() ? (
                <>
                  <h3 className={styles.sectionTitle}>Exclusions</h3>
                  <p className={styles.prose}>{proposal.exclusions}</p>
                </>
              ) : null}

              {proposal.notes?.trim() ? (
                <>
                  <h3 className={styles.sectionTitle}>Notes from the guide</h3>
                  <p className={styles.prose}>{proposal.notes}</p>
                </>
              ) : null}

              <div className={styles.cardFooter}>
                {proposal.isEligibleForAcceptance ? (
                  <button
                    className={styles.primaryButton}
                    onClick={() => {
                      setActionError(null)
                      setIsConfirming(true)
                    }}
                    type="button"
                  >
                    Accept this proposal
                  </button>
                ) : (
                  <p className={styles.hintText}>
                    This proposal can no longer be accepted. The API only accepts submitted
                    proposals on trips that are still open.
                  </p>
                )}

                <Link className={styles.secondaryButton} to={`/trips/${tripRouteId}`}>
                  Open trip request
                </Link>
              </div>
            </section>

            {itinerary.length > 0 ? (
              <section className={styles.panel} aria-label="Proposed itinerary">
                <h2 className={styles.sectionTitle}>Proposed itinerary</h2>
                <ol className={styles.itineraryList}>
                  {itinerary.map((day, index) => (
                    <li className={styles.itineraryDay} key={`${day.dayNumber}-${index}`}>
                      <p className={styles.itineraryDayTitle}>
                        Day {day.dayNumber}
                        {day.title ? ` — ${day.title}` : ''}
                      </p>
                      {day.description ? (
                        <p className={styles.itineraryDayBody}>{day.description}</p>
                      ) : null}
                      {day.activities ? (
                        <p className={styles.itineraryDayMeta}>Activities: {day.activities}</p>
                      ) : null}
                      {day.accommodation ? (
                        <p className={styles.itineraryDayMeta}>
                          Accommodation: {day.accommodation}
                        </p>
                      ) : null}
                      {day.meals ? (
                        <p className={styles.itineraryDayMeta}>Meals: {day.meals}</p>
                      ) : null}
                    </li>
                  ))}
                </ol>
              </section>
            ) : null}

            <div className={styles.panel}>
              {breakdownResource.status === LOADING ? (
                <div className={styles.skeletonPanel} aria-hidden="true" />
              ) : null}

              {breakdownResource.status === ERROR ? (
                <StateNotice
                  actionLabel="Retry breakdown"
                  detail={describeApiError(
                    breakdownResource.error,
                    'The cost breakdown could not be loaded.',
                  )}
                  onAction={breakdownResource.reload}
                  title="Cost breakdown unavailable"
                  tone="error"
                />
              ) : null}

              {breakdownResource.status === READY && breakdownResource.data ? (
                <CostBreakdownCard breakdown={breakdownResource.data} />
              ) : null}
            </div>

            {showPaymentSection && (
              <section className={styles.panel} aria-label="Payment">
                <div className={styles.panelHead}>
                  <h2 className={styles.sectionTitle}>Payment</h2>
                </div>

                <p className={styles.sectionLead}>
                  The proposal has been accepted. Pay the deposit to place funds in simulated escrow and move the trip forward.
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
                          {formatCurrency(paymentData?.amount ?? paymentData?.Amount, paymentData?.currency ?? paymentData?.Currency ?? 'USD')}
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
                            {getMilestones(paymentData).map((milestone) => (
                              <li
                                className={styles.milestoneItem}
                                key={milestone.id ?? milestone.MilestoneId}
                              >
                                <span className={styles.milestoneLabel}>
                                  {milestone.label ?? milestone.Label}: {formatCurrency(milestone.amount ?? milestone.Amount, milestone.currency ?? paymentData?.currency ?? paymentData?.Currency ?? 'USD')}
                                </span>
                                <span className={styles.milestoneStatus}>
                                  {milestoneStatusLabel(milestone.status ?? milestone.Status)}
                                </span>
                              </li>
                            ))}
                          </ul>
                        </div>
                      )}
                      {isPaymentHeld && (
                        <p className={styles.breakdownNote}>
                          Funds are held in simulated escrow. They will be released to the guide after the trip is completed.
                        </p>
                      )}
                    </div>

                    <div className={styles.cardFooter}>
                      {canRetryPayment && (
                        <button
                          className={styles.primaryButton}
                          onClick={handlePay}
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

                      <Link className={styles.secondaryButton} to={`/trips/${tripRouteId}`}>
                        Open trip request
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
                    {paymentFeedback ? (
                      <FeedbackBanner tone={paymentFeedback.tone}>{paymentFeedback.message}</FeedbackBanner>
                    ) : null}

                    <div className={styles.cardFooter}>
                      <button
                        className={styles.primaryButton}
                        onClick={handlePay}
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

                      <Link className={styles.secondaryButton} to={`/trips/${tripRouteId}`}>
                        Open trip request
                      </Link>
                    </div>

                    {paymentError && !isPaying && (
                      <p className={styles.hintText} style={{ color: 'var(--tm-danger-600)' }}>
                        {paymentError}
                      </p>
                    )}
                  </>
                )}
              </section>
            )}
          </>
        ) : null}
      </div>

      <ConfirmDialog
        busy={isSubmitting}
        cancelLabel="Not now"
        confirmLabel="Accept proposal"
        description="Accepting this proposal matches the trip with this guide. The API declines every other submitted proposal, and the decision cannot be undone from the app."
        onCancel={() => {
          if (isSubmitting) return
          setIsConfirming(false)
          setActionError(null)
        }}
        onConfirm={handleAccept}
        open={isConfirming}
        title="Accept this proposal?"
      >
        {actionError ? <FeedbackBanner tone="error">{actionError}</FeedbackBanner> : null}
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default ProposalDetailPage
