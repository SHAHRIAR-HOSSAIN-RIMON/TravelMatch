import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import ProposalStatusPill from '../components/guide/ProposalStatusPill.jsx'
import ProposalForm from '../components/guide/ProposalForm.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from '../lib/api.js'
import {
  getMyProposals,
  getProposalItinerary,
  getProposalCostBreakdown,
  updateProposal,
  withdrawProposal,
} from '../lib/guideService.js'
import styles from '../components/guide/Guide.module.css'

export function GuideProposalDetailPage() {
  const { proposalId: routeProposalId } = useParams()
  const { user, token, logout } = useAuth()

  const proposalId = Number(routeProposalId)

  const [loading, setLoading] = useState(true)
  const [proposal, setProposal] = useState(null)
  const [itinerary, setItinerary] = useState(null)
  const [costBreakdown, setCostBreakdown] = useState(null)
  const [isEditing, setIsEditing] = useState(false)
  const [isUpdating, setIsUpdating] = useState(false)
  const [isWithdrawing, setIsWithdrawing] = useState(false)
  const [showWithdrawDialog, setShowWithdrawDialog] = useState(false)
  const [feedback, setFeedback] = useState(null)
  const [error, setError] = useState(null)

  const requestIdRef = useRef(0)

  const loadData = useCallback(async () => {
    if (!token || !proposalId) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const myProposals = await getMyProposals(token)
      if (requestId !== requestIdRef.current) return

      const found = myProposals.find((p) => p.id === proposalId)

      if (!found) {
        setError('Proposal not found.')
        if (requestId === requestIdRef.current) {
          setLoading(false)
        }
        return
      }

      setProposal(found)

      // Fetch itinerary days if any
      try {
        const itinData = await getProposalItinerary(token, proposalId)
        if (requestId !== requestIdRef.current) return
        setItinerary(itinData?.itineraryDays || [])
      } catch {
        // Itinerary is optional
        if (requestId === requestIdRef.current) {
          setItinerary([])
        }
      }

      // Fetch cost breakdown if proposal is accepted
      if (found.status === 'Accepted') {
        try {
          const breakdown = await getProposalCostBreakdown(token, proposalId)
          if (requestId !== requestIdRef.current) return
          setCostBreakdown(breakdown)
        } catch {
          // Cost breakdown may not be ready if unpaid
          if (requestId === requestIdRef.current) {
            setCostBreakdown(null)
          }
        }
      }
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      if (err instanceof ApiError && err.status === 401) {
        logout()
        return
      }
      setError(err instanceof Error ? err.message : 'Failed to load proposal details.')
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [logout, proposalId, token])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await loadData()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [loadData])

  async function handleUpdate(formValues) {
    if (!token || !proposal || isUpdating) return

    setIsUpdating(true)
    setFeedback(null)

    try {
      const updated = await updateProposal(token, proposal.tripRequestId, proposal.id, formValues)
      setFeedback({
        tone: 'success',
        message: 'Proposal updated successfully!',
      })
      setProposal((prev) => ({
        ...prev,
        ...updated,
        proposedPrice: formValues.proposedPrice ? Number(formValues.proposedPrice) : prev.proposedPrice,
        message: formValues.message !== undefined ? formValues.message : prev.message,
        availability: formValues.availability !== undefined ? formValues.availability : prev.availability,
        inclusions: formValues.inclusions !== undefined ? formValues.inclusions : prev.inclusions,
        exclusions: formValues.exclusions !== undefined ? formValues.exclusions : prev.exclusions,
        notes: formValues.notes !== undefined ? formValues.notes : prev.notes,
      }))
      setIsEditing(false)
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: err instanceof ApiError ? describeApiError(err) : 'Failed to update proposal.',
      })
    } finally {
      setIsUpdating(false)
    }
  }

  async function handleWithdraw() {
    if (!token || !proposal || isWithdrawing) return

    setIsWithdrawing(true)
    setFeedback(null)

    try {
      await withdrawProposal(token, proposal.tripRequestId, proposal.id)
      setFeedback({
        tone: 'success',
        message: 'Proposal withdrawn successfully.',
      })
      setProposal((prev) => ({ ...prev, status: 'Withdrawn' }))
      setShowWithdrawDialog(false)
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: err instanceof ApiError ? describeApiError(err) : 'Failed to withdraw proposal.',
      })
    } finally {
      setIsWithdrawing(false)
    }
  }

  const isEditable = proposal?.status === 'Draft' || proposal?.status === 'Submitted'
  const isWithdrawable = proposal?.status === 'Draft' || proposal?.status === 'Submitted'

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backTo="/guide/proposals"
          backLabel="My Proposals"
          eyebrow="Proposal Details"
          title={proposal ? `${proposal.tripRequestDestination || `Proposal #${proposal.id}`}` : 'Proposal Details'}
          description="View complete terms, pitch notes, and itinerary for this proposal."
        >
          {isEditable && !isEditing ? (
            <button
              className={styles.primaryButton}
              onClick={() => setIsEditing(true)}
              type="button"
            >
              Edit Proposal
            </button>
          ) : null}

          {isWithdrawable ? (
            <button
              className={styles.dangerButton}
              onClick={() => setShowWithdrawDialog(true)}
              type="button"
            >
              Withdraw Proposal
            </button>
          ) : null}
        </PageHeading>

        {/* Feedback message */}
        {feedback ? (
          <div
            className={`${styles.noticeBanner} ${
              feedback.tone === 'success' ? styles.noticeSuccess : styles.noticeError
            }`}
            role={feedback.tone === 'success' ? 'status' : 'alert'}
          >
            <span className={styles.noticeIcon} aria-hidden="true">
              {feedback.tone === 'success' ? '✓' : '!'}
            </span>
            <div>{feedback.message}</div>
          </div>
        ) : null}

        {/* Error message */}
        {error ? (
          <div className={`${styles.noticeBanner} ${styles.noticeError}`} role="alert">
            <span className={styles.noticeIcon} aria-hidden="true">!</span>
            <div style={{ flex: 1 }}>{error}</div>
            <button className={styles.secondaryButton} onClick={loadData} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {loading ? (
          <div className={styles.detailGrid}>
            <div className={styles.skeletonCard} style={{ height: '400px' }} />
            <div className={styles.skeletonCard} style={{ height: '400px' }} />
          </div>
        ) : proposal ? (
          <div className={styles.detailGrid}>
            {/* Left: Proposal Content or Edit Form */}
            <div className={styles.detailMainCard}>
              <div className={styles.detailHeader}>
                <div>
                  <h2 style={{ margin: 0, fontSize: '22px', fontWeight: 700 }}>
                    {proposal.tripRequestDestination || `Trip Request #${proposal.tripRequestId}`}
                  </h2>
                  <span className={styles.metaLabel} style={{ marginTop: '4px', display: 'block' }}>
                    Trip Dates: {proposal.tripRequestStartDate} to {proposal.tripRequestEndDate}
                  </span>
                </div>
                <ProposalStatusPill status={proposal.status} />
              </div>

              {/* Status context callout */}
              {proposal.status === 'Accepted' ? (
                <div className={`${styles.noticeBanner} ${styles.noticeSuccess}`}>
                  <span className={styles.noticeIcon} aria-hidden="true">🎉</span>
                  <div>
                    <strong>Proposal Accepted:</strong> The tourist accepted this proposal! View your assigned trip in <Link to="/guide/trips" style={{ textDecoration: 'underline', fontWeight: 600 }}>My Assigned Trips</Link>.
                  </div>
                </div>
              ) : proposal.status === 'Withdrawn' ? (
                <div className={`${styles.noticeBanner} ${styles.noticeInfo}`}>
                  <span className={styles.noticeIcon} aria-hidden="true">ℹ️</span>
                  <div>This proposal was withdrawn. The tourist cannot accept it.</div>
                </div>
              ) : proposal.status === 'Rejected' ? (
                <div className={`${styles.noticeBanner} ${styles.noticeWarning}`}>
                  <span className={styles.noticeIcon} aria-hidden="true">ℹ️</span>
                  <div>Another proposal was selected for this trip request.</div>
                </div>
              ) : null}

              {isEditing ? (
                <div>
                  <h3 style={{ margin: '0 0 16px 0', fontSize: '18px', fontWeight: 700 }}>
                    Edit Your Proposal
                  </h3>
                  <ProposalForm
                    initialValues={{
                      proposedPrice: proposal.proposedPrice,
                      message: proposal.message,
                      availability: proposal.availability,
                      inclusions: proposal.inclusions,
                      exclusions: proposal.exclusions,
                      notes: proposal.notes,
                    }}
                    isEditing={true}
                    isSubmitting={isUpdating}
                    onCancel={() => setIsEditing(false)}
                    onSubmit={handleUpdate}
                  />
                </div>
              ) : (
                <>
                  <div className={styles.contentBlock}>
                    <h3>Proposal Pitch & Description</h3>
                    <p>{proposal.message || 'No description provided.'}</p>
                  </div>

                  <div className={styles.contentBlock}>
                    <h3>Availability</h3>
                    <p>{proposal.availability || 'No specific availability details.'}</p>
                  </div>

                  <div className={styles.contentBlock}>
                    <h3>Inclusions</h3>
                    <p>{proposal.inclusions || 'No inclusions specified.'}</p>
                  </div>

                  <div className={styles.contentBlock}>
                    <h3>Exclusions</h3>
                    <p>{proposal.exclusions || 'No exclusions specified.'}</p>
                  </div>

                  {proposal.notes ? (
                    <div className={styles.contentBlock}>
                      <h3>Additional Notes</h3>
                      <p>{proposal.notes}</p>
                    </div>
                  ) : null}

                  {/* Planned Itinerary Days */}
                  {itinerary && itinerary.length > 0 ? (
                    <div className={styles.contentBlock} style={{ marginTop: '28px', borderTop: '1px solid var(--tm-border-soft)', paddingTop: '20px' }}>
                      <h3>Planned Itinerary ({itinerary.length} Day(s))</h3>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px' }}>
                        {itinerary.map((day) => (
                          <div key={day.id || day.dayNumber} style={{ padding: '14px', background: 'var(--tm-slate-50)', borderRadius: '8px' }}>
                            <strong style={{ display: 'block', fontSize: '14px', color: 'var(--tm-slate-900)' }}>
                              Day {day.dayNumber}: {day.title}
                            </strong>
                            {day.schedule ? (
                              <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: 'var(--tm-slate-600)' }}>
                                Schedule: {day.schedule}
                              </p>
                            ) : day.description ? (
                              <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: 'var(--tm-slate-600)' }}>
                                {day.description}
                              </p>
                            ) : null}
                            {day.activities ? (
                              <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--tm-slate-500)' }}>
                                Activities: {day.activities}
                              </p>
                            ) : null}
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </>
              )}
            </div>

            {/* Right: Proposal Summary & Financials */}
            <div className={styles.detailSideCard}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '18px', fontWeight: 700 }}>
                Proposal Overview
              </h3>

              <div className={styles.detailMetaList} style={{ gridTemplateColumns: '1fr', marginBottom: '20px' }}>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Quoted Price</span>
                  <span className={styles.metaValue} style={{ fontSize: '24px', color: 'var(--tm-teal-800)' }}>
                    ${proposal.proposedPrice}
                  </span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Current Status</span>
                  <span className={styles.metaValue}>{proposal.status}</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Submitted At</span>
                  <span className={styles.metaValue}>
                    {new Date(proposal.createdAt).toLocaleString()}
                  </span>
                </div>
                {proposal.updatedAt ? (
                  <div className={styles.metaItem}>
                    <span className={styles.metaLabel}>Last Updated</span>
                    <span className={styles.metaValue}>
                      {new Date(proposal.updatedAt).toLocaleString()}
                    </span>
                  </div>
                ) : null}
              </div>

              {/* Related Trip Request CTA */}
              <div style={{ marginBottom: '20px' }}>
                <Link
                  className={styles.secondaryButton}
                  style={{ width: '100%' }}
                  to={`/guide/trip-requests/${proposal.tripRequestId}`}
                >
                  View Related Trip Request →
                </Link>
              </div>

              {/* Itemized Cost Breakdown (when accepted) */}
              {costBreakdown ? (
                <div className={styles.breakdownCard}>
                  <h4 className={styles.breakdownTitle}>Itemized Cost Breakdown</h4>
                  <div className={styles.breakdownRow}>
                    <span>Base Quoted Price</span>
                    <span>${costBreakdown.basePrice}</span>
                  </div>
                  <div className={styles.breakdownRow}>
                    <span>Travelers</span>
                    <span>{costBreakdown.numberOfTravelers}</span>
                  </div>
                  <div className={styles.breakdownRow}>
                    <span>Per Traveler</span>
                    <span>${costBreakdown.pricePerPerson}</span>
                  </div>
                  <div className={`${styles.breakdownRow} ${styles.breakdownRowTotal}`}>
                    <span>Total Amount</span>
                    <span>${costBreakdown.totalAmount} {costBreakdown.currency}</span>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        ) : null}

        {/* Withdraw Confirmation Dialog */}
        <ConfirmDialog
          busy={isWithdrawing}
          confirmLabel="Yes, Withdraw"
          description="Are you sure you want to withdraw this proposal? Once withdrawn, the tourist will no longer be able to accept it."
          onCancel={() => setShowWithdrawDialog(false)}
          onConfirm={handleWithdraw}
          open={showWithdrawDialog}
          title="Withdraw Proposal?"
          tone="danger"
        />
      </div>
    </DashboardLayout>
  )
}

export default GuideProposalDetailPage

