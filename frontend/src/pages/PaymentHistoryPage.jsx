import { useState, useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/useAuth.js'
import useResource, { LOADING, ERROR, READY } from '../lib/useResource.js'
import { describeApiError } from '../lib/api.js'
import {
  getMyPayments,
  releaseEscrow,
  releaseMilestone,
  isEscrowHeld,
  isEscrowReleased,
  PAYMENT_STATUS,
  MILESTONE_STATUS,
  PAYMENT_TARGET_TYPE,
  normalizePaymentStatus,
  normalizeMilestoneStatus,
  describePaymentError,
} from '../lib/paymentService.js'
import { formatCurrency } from '../components/dashboard/dashboardModel.js'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'

import touristStyles from '../components/tourist/Tourist.module.css'
import styles from './PaymentHistory.module.css'

function PaymentHistoryPage() {
  const { user, token, logout } = useAuth()

  // State
  const [statusFilter, setStatusFilter] = useState('all') // 'all' | 'held' | 'released' | 'refunded'
  const [targetFilter, setTargetFilter] = useState('all') // 'all' | 'proposal' | 'registration'
  const [pendingRelease, setPendingRelease] = useState(null) // { paymentId, milestoneId?, label?, amount? }
  const [isReleasing, setIsReleasing] = useState(false)
  const [actionFeedback, setActionFeedback] = useState(null)
  const [actionError, setActionError] = useState(null)

  const loader = useCallback(async () => {
    return getMyPayments(token)
  }, [token])

  const { data: payments = [], status, error, reload } = useResource(loader, logout)

  // Calculations for summary metrics
  const metrics = useMemo(() => {
    let totalDeposited = 0
    let heldInEscrow = 0
    let releasedEscrow = 0
    let refundedTotal = 0

    payments.forEach((p) => {
      const amt = Number(p.amount) || 0
      const pStatus = normalizePaymentStatus(p.status)

      if (pStatus !== PAYMENT_STATUS.Failed) {
        totalDeposited += amt
      }

      if (pStatus === PAYMENT_STATUS.Held || pStatus === PAYMENT_STATUS.PartiallyRefunded) {
        heldInEscrow += amt - (Number(p.refundedAmount) || 0)
      } else if (pStatus === PAYMENT_STATUS.Released || pStatus === PAYMENT_STATUS.Completed) {
        releasedEscrow += amt
      }

      if (p.refundedAmount != null) {
        refundedTotal += Number(p.refundedAmount) || 0
      }
    })

    return {
      totalDeposited,
      heldInEscrow,
      releasedEscrow,
      refundedTotal,
      totalCount: payments.length,
    }
  }, [payments])

  // Filtered payments list
  const filteredPayments = useMemo(() => {
    return payments.filter((p) => {
      // Status filter
      if (statusFilter === 'held' && !isEscrowHeld(p)) return false
      if (statusFilter === 'released' && !isEscrowReleased(p)) return false
      if (statusFilter === 'refunded') {
        const s = normalizePaymentStatus(p.status)
        if (s !== PAYMENT_STATUS.Refunded && s !== PAYMENT_STATUS.PartiallyRefunded) return false
      }

      // Target filter
      if (targetFilter === 'proposal' && p.targetType !== PAYMENT_TARGET_TYPE.Proposal) return false
      if (targetFilter === 'registration' && p.targetType !== PAYMENT_TARGET_TYPE.Registration) return false

      return true
    })
  }, [payments, statusFilter, targetFilter])

  // Handlers for escrow release
  function handleOpenRelease(payment, milestone = null) {
    setActionError(null)
    setActionFeedback(null)
    setPendingRelease({
      paymentId: payment.id,
      milestoneId: milestone?.id ?? null,
      label: milestone ? `Milestone: ${milestone.label}` : 'Full Escrow Deposit',
      amount: milestone ? milestone.amount : payment.amount,
      currency: payment.currency,
    })
  }

  function handleCloseRelease() {
    if (isReleasing) return
    setPendingRelease(null)
    setActionError(null)
  }

  async function handleConfirmRelease() {
    if (!pendingRelease || isReleasing) return

    setIsReleasing(true)
    setActionError(null)

    try {
      if (pendingRelease.milestoneId) {
        await releaseMilestone(token, pendingRelease.paymentId, pendingRelease.milestoneId)
        setActionFeedback({
          tone: 'success',
          message: `Milestone released successfully! Funds of ${formatCurrency(
            pendingRelease.amount,
            pendingRelease.currency,
          )} have been released.`,
        })
      } else {
        await releaseEscrow(token, pendingRelease.paymentId)
        setActionFeedback({
          tone: 'success',
          message: `Escrow released successfully! Funds of ${formatCurrency(
            pendingRelease.amount,
            pendingRelease.currency,
          )} have been transferred.`,
        })
      }

      setPendingRelease(null)
      reload()
    } catch (err) {
      const msg = describePaymentError(err, 'The escrow release could not be completed.')
      setActionError(msg)
    } finally {
      setIsReleasing(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backLabel="Dashboard"
          backTo="/dashboard"
          description="View simulated payments, track funds protected in escrow, and release deposits once trips are completed."
          title="Payment History & Escrow"
        />

        {actionFeedback && (
          <FeedbackBanner tone={actionFeedback.tone}>{actionFeedback.message}</FeedbackBanner>
        )}

        {/* Summary Stats Band */}
        <section className={styles.statsGrid} aria-label="Payment summary">
          <div className={styles.statCard}>
            <span className={styles.statLabel}>Total Deposited</span>
            <span className={styles.statValue}>
              {formatCurrency(metrics.totalDeposited, 'USD')}
            </span>
            <span className={styles.statSubtext}>Across all transactions</span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>Held in Escrow</span>
            <span className={styles.statValue} style={{ color: 'var(--tm-teal-700, #0f766e)' }}>
              {formatCurrency(metrics.heldInEscrow, 'USD')}
            </span>
            <span className={styles.statSubtext}>Protected until trip completion</span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>Released Escrow</span>
            <span className={styles.statValue} style={{ color: 'var(--tm-text-secondary, #334155)' }}>
              {formatCurrency(metrics.releasedEscrow, 'USD')}
            </span>
            <span className={styles.statSubtext}>Settled to guides & organizers</span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>Transactions</span>
            <span className={styles.statValue}>{metrics.totalCount}</span>
            <span className={styles.statSubtext}>Total records recorded</span>
          </div>
        </section>

        {/* Filters and Controls */}
        <div className={styles.filtersBar}>
          <div className={styles.filterGroup}>
            <button
              type="button"
              className={`${touristStyles.filter} ${statusFilter === 'all' ? touristStyles.filterActive : ''}`}
              onClick={() => setStatusFilter('all')}
            >
              All Statuses <span className={touristStyles.filterCount}>({payments.length})</span>
            </button>
            <button
              type="button"
              className={`${touristStyles.filter} ${statusFilter === 'held' ? touristStyles.filterActive : ''}`}
              onClick={() => setStatusFilter('held')}
            >
              Held in Escrow
            </button>
            <button
              type="button"
              className={`${touristStyles.filter} ${statusFilter === 'released' ? touristStyles.filterActive : ''}`}
              onClick={() => setStatusFilter('released')}
            >
              Released
            </button>
            <button
              type="button"
              className={`${touristStyles.filter} ${statusFilter === 'refunded' ? touristStyles.filterActive : ''}`}
              onClick={() => setStatusFilter('refunded')}
            >
              Refunded
            </button>
          </div>

          <div className={styles.filterGroup}>
            <button
              type="button"
              className={`${touristStyles.filter} ${targetFilter === 'all' ? touristStyles.filterActive : ''}`}
              onClick={() => setTargetFilter('all')}
            >
              All Types
            </button>
            <button
              type="button"
              className={`${touristStyles.filter} ${targetFilter === 'proposal' ? touristStyles.filterActive : ''}`}
              onClick={() => setTargetFilter('proposal')}
            >
              Custom Proposals
            </button>
            <button
              type="button"
              className={`${touristStyles.filter} ${targetFilter === 'registration' ? touristStyles.filterActive : ''}`}
              onClick={() => setTargetFilter('registration')}
            >
              Group Registrations
            </button>
          </div>
        </div>

        {/* Main Content States */}
        {status === LOADING && (
          <div className={styles.paymentsList}>
            <div className={touristStyles.skeletonCard} />
            <div className={touristStyles.skeletonCard} />
          </div>
        )}

        {status === ERROR && (
          <StateNotice
            actionLabel="Try again"
            detail={describeApiError(error, 'Could not load your payment records.')}
            onAction={reload}
            title="Failed to load payments"
            tone="error"
          />
        )}

        {status === READY && filteredPayments.length === 0 && (
          <div className={touristStyles.emptyNotice}>
            <h3 className={touristStyles.emptyTitle}>No payment records found</h3>
            <p className={touristStyles.emptyDetail}>
              {payments.length === 0
                ? 'You have not made any simulated payments yet. Accepted proposals and organized trip registrations will appear here once deposited.'
                : 'No payments match your selected filters. Try choosing a different status or payment type.'}
            </p>
            <div style={{ marginTop: '1rem', display: 'flex', gap: '0.75rem', justifyContent: 'center' }}>
              <Link className={touristStyles.primaryButton} to="/trips">
                View My Trips
              </Link>
              <Link className={touristStyles.secondaryButton} to="/organized-trips">
                Explore Group Trips
              </Link>
            </div>
          </div>
        )}

        {status === READY && filteredPayments.length > 0 && (
          <div className={styles.paymentsList}>
            {filteredPayments.map((payment) => {
              const isHeld = isEscrowHeld(payment)
              const isReleased = isEscrowReleased(payment)
              const hasMilestones = payment.milestones && payment.milestones.length > 0

              return (
                <article key={payment.id} className={styles.paymentCard}>
                  <div className={styles.cardTop}>
                    <div className={styles.cardMeta}>
                      <span className={styles.targetBadge}>
                        {payment.targetTypeLabel}
                      </span>
                      <h3 className={styles.paymentTitle}>
                        Payment #{payment.id}
                        {payment.proposalId && ` — Proposal #${payment.proposalId}`}
                        {payment.registrationId && ` — Registration #${payment.registrationId}`}
                      </h3>
                      {payment.transactionReference && (
                        <span className={styles.transactionRef}>
                          Ref: {payment.transactionReference}
                        </span>
                      )}
                    </div>

                    <div className={styles.amountBox}>
                      <span className={styles.amount}>
                        {formatCurrency(payment.amount, payment.currency)}
                      </span>
                      <StatusPill {...payment.statusMeta} />
                    </div>
                  </div>

                  {/* Transaction Details Grid */}
                  <div className={styles.detailsGrid}>
                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Initiated</span>
                      <span className={styles.detailValue}>
                        {payment.createdAt ? new Date(payment.createdAt).toLocaleString() : '—'}
                      </span>
                    </div>

                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Deposited in Escrow</span>
                      <span className={styles.detailValue}>
                        {payment.completedAt ? new Date(payment.completedAt).toLocaleString() : '—'}
                      </span>
                    </div>

                    {payment.escrowReleasedAt && (
                      <div className={styles.detailItem}>
                        <span className={styles.detailLabel}>Escrow Released</span>
                        <span className={styles.detailValue}>
                          {new Date(payment.escrowReleasedAt).toLocaleString()}
                        </span>
                      </div>
                    )}

                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Protection State</span>
                      <span className={styles.detailValue}>
                        {isHeld
                          ? 'Protected in Escrow'
                          : isReleased
                          ? 'Released & Settled'
                          : payment.statusMeta.label}
                      </span>
                    </div>
                  </div>

                  {/* Refund Notice */}
                  {payment.refundedAmount != null && (
                    <div className={styles.refundAlert}>
                      <strong>Refund Issued:</strong> {formatCurrency(payment.refundedAmount, payment.currency)}
                      {payment.refundedAt && ` on ${new Date(payment.refundedAt).toLocaleString()}`}
                      {payment.refundReason && ` — Reason: "${payment.refundReason}"`}
                    </div>
                  )}

                  {/* Failure Reason */}
                  {payment.failureReason && (
                    <div className={touristStyles.feedbackError}>
                      <strong>Payment issue:</strong> {payment.failureReason}
                    </div>
                  )}

                  {/* Milestones Section */}
                  {hasMilestones && (
                    <div className={styles.milestonesContainer}>
                      <div className={styles.milestonesHeader}>Payment Milestones</div>
                      {payment.milestones.map((milestone) => {
                        const mStatus = normalizeMilestoneStatus(milestone.status)
                        const isMilestoneHeld = mStatus === MILESTONE_STATUS.Held

                        return (
                          <div key={milestone.id} className={styles.milestoneRow}>
                            <div className={styles.milestoneInfo}>
                              <strong>{milestone.label}:</strong>
                              <span>{formatCurrency(milestone.amount, payment.currency)}</span>
                            </div>

                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                              <StatusPill
                                label={milestone.statusLabel}
                                tone={isMilestoneHeld ? 'attention' : 'progress'}
                              />

                              {isMilestoneHeld && isHeld && (
                                <button
                                  type="button"
                                  className={touristStyles.milestoneReleaseButton}
                                  onClick={() => handleOpenRelease(payment, milestone)}
                                >
                                  Release milestone
                                </button>
                              )}
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}

                  {/* Escrow Status & Action */}
                  {isHeld && (
                    <div className={styles.escrowNotice}>
                      <span>
                        🛡️ <strong>Simulated Escrow:</strong> Funds remain securely held until the trip is completed and confirmed.
                      </span>

                      {!hasMilestones && (
                        <button
                          type="button"
                          className={touristStyles.primaryButton}
                          onClick={() => handleOpenRelease(payment)}
                        >
                          Release escrow
                        </button>
                      )}
                    </div>
                  )}

                  {/* Footer Navigation */}
                  <div className={styles.cardFooter}>
                    <div className={styles.actionLinks}>
                      {payment.proposalId && (
                        <Link
                          className={touristStyles.secondaryButton}
                          to={`/proposals`}
                        >
                          View Proposals
                        </Link>
                      )}

                      {payment.registrationId && (
                        <Link
                          className={touristStyles.secondaryButton}
                          to={`/registrations`}
                        >
                          View Registrations
                        </Link>
                      )}
                    </div>

                    <span className={touristStyles.hintText}>
                      Protected by TravelMatch Escrow Simulation
                    </span>
                  </div>
                </article>
              )
            })}
          </div>
        )}

        {/* Release Escrow / Milestone Confirmation Modal */}
        <ConfirmDialog
          busy={isReleasing}
          cancelLabel="Keep in Escrow"
          confirmLabel={pendingRelease?.milestoneId ? 'Release Milestone' : 'Release Escrow'}
          description={
            pendingRelease?.milestoneId
              ? `Are you sure you want to release ${formatCurrency(
                  pendingRelease?.amount,
                  pendingRelease?.currency,
                )} for ${pendingRelease?.label}? This will transfer the milestone funds to the guide/organizer.`
              : `Are you sure you want to release the full deposit of ${formatCurrency(
                  pendingRelease?.amount,
                  pendingRelease?.currency,
                )}? The associated trip must be marked as completed and have no active disputes. This action cannot be reversed.`
          }
          onCancel={handleCloseRelease}
          onConfirm={handleConfirmRelease}
          open={Boolean(pendingRelease)}
          title={pendingRelease?.milestoneId ? 'Release Payment Milestone?' : 'Release Escrow Deposit?'}
        >
          {actionError && <FeedbackBanner tone="error">{actionError}</FeedbackBanner>}
        </ConfirmDialog>
      </div>
    </DashboardLayout>
  )
}

export default PaymentHistoryPage

