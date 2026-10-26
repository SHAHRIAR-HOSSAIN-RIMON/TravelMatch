import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import ProposalStatusPill from '../components/guide/ProposalStatusPill.jsx'
import ConfirmDialog from '../components/tourist/ConfirmDialog.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from '../lib/api.js'
import { getMyProposals, withdrawProposal } from '../lib/guideService.js'
import styles from '../components/guide/Guide.module.css'

const STATUS_TABS = [
  { id: 'all', label: 'All Proposals' },
  { id: 'Submitted', label: 'Submitted' },
  { id: 'Accepted', label: 'Accepted' },
  { id: 'Draft', label: 'Draft' },
  { id: 'closed', label: 'Closed / Withdrawn' },
]

export function GuideProposalsPage() {
  const { user, token, logout } = useAuth()

  const [loading, setLoading] = useState(true)
  const [proposals, setProposals] = useState([])
  const [error, setError] = useState(null)
  const [activeTab, setActiveTab] = useState('all')
  const [withdrawTarget, setWithdrawTarget] = useState(null)
  const [isWithdrawing, setIsWithdrawing] = useState(false)
  const [feedback, setFeedback] = useState(null)

  const fetchProposals = useCallback(async () => {
    if (!token) return

    setLoading(true)
    setError(null)

    try {
      const data = await getMyProposals(token)
      setProposals(data || [])
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout()
        return
      }
      setError(err instanceof Error ? err.message : 'Failed to load proposals.')
    } finally {
      setLoading(false)
    }
  }, [logout, token])

  useEffect(() => {
    fetchProposals()
  }, [fetchProposals])

  // Status counts
  const counts = useMemo(() => {
    const res = { all: proposals.length, Submitted: 0, Accepted: 0, Draft: 0, closed: 0 }
    proposals.forEach((p) => {
      if (p.status === 'Submitted') res.Submitted += 1
      else if (p.status === 'Accepted') res.Accepted += 1
      else if (p.status === 'Draft') res.Draft += 1
      else if (p.status === 'Withdrawn' || p.status === 'Rejected') res.closed += 1
    })
    return res
  }, [proposals])

  // Filtered proposals
  const filteredProposals = useMemo(() => {
    if (activeTab === 'all') return proposals
    if (activeTab === 'closed') {
      return proposals.filter((p) => p.status === 'Withdrawn' || p.status === 'Rejected')
    }
    return proposals.filter((p) => p.status === activeTab)
  }, [activeTab, proposals])

  async function handleConfirmWithdraw() {
    if (!token || !withdrawTarget || isWithdrawing) return

    setIsWithdrawing(true)
    setFeedback(null)

    try {
      await withdrawProposal(token, withdrawTarget.tripRequestId, withdrawTarget.id)
      setFeedback({
        tone: 'success',
        message: 'Your proposal was withdrawn successfully.',
      })
      setWithdrawTarget(null)
      fetchProposals()
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: err instanceof ApiError ? describeApiError(err) : 'Failed to withdraw proposal.',
      })
    } finally {
      setIsWithdrawing(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backTo="/guide/dashboard"
          backLabel="Guide Dashboard"
          eyebrow="Guide Module"
          title="My Submitted Proposals"
          description="Track the status of all offers you have submitted to travelers. Review accepted trips, update drafts, or withdraw pending bids."
        >
          <Link className={styles.primaryButton} to="/guide/trip-requests">
            Browse New Requests
          </Link>
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

        {/* Error Notice */}
        {error ? (
          <div className={`${styles.noticeBanner} ${styles.noticeError}`} role="alert">
            <span className={styles.noticeIcon} aria-hidden="true">!</span>
            <div style={{ flex: 1 }}>{error}</div>
            <button className={styles.secondaryButton} onClick={fetchProposals} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {/* Tab List */}
        <div className={styles.tabList} role="tablist">
          {STATUS_TABS.map((tab) => (
            <button
              aria-selected={activeTab === tab.id}
              className={`${styles.tabButton} ${activeTab === tab.id ? styles.tabActive : ''}`}
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              role="tab"
              type="button"
            >
              <span>{tab.label}</span>
              <span className={styles.tabCount}>{counts[tab.id] ?? 0}</span>
            </button>
          ))}
        </div>

        {/* Proposals Grid */}
        {loading ? (
          <div className={styles.cardGrid}>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </div>
        ) : filteredProposals.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>
              {activeTab === 'all'
                ? 'No proposals submitted yet'
                : `No ${activeTab.toLowerCase()} proposals`}
            </h3>
            <p className={styles.emptyText}>
              {activeTab === 'all'
                ? 'Explore open trip requests and send custom offers to travelers.'
                : `You do not have any proposals in the '${activeTab}' category right now.`}
            </p>
            <Link className={styles.primaryButton} to="/guide/trip-requests">
              Browse Open Trip Requests
            </Link>
          </div>
        ) : (
          <div className={styles.cardGrid}>
            {filteredProposals.map((proposal) => {
              const canWithdraw = proposal.status === 'Submitted' || proposal.status === 'Draft'

              return (
                <article className={styles.proposalCard} key={proposal.id}>
                  <div>
                    <div className={styles.cardTop}>
                      <div>
                        <h3 className={styles.cardDestination}>
                          {proposal.tripRequestDestination || `Trip Request #${proposal.tripRequestId}`}
                        </h3>
                        <span className={styles.metaLabel}>
                          {proposal.tripRequestStartDate} to {proposal.tripRequestEndDate}
                        </span>
                      </div>
                      <ProposalStatusPill status={proposal.status} />
                    </div>

                    <div className={styles.cardMetaGrid} style={{ marginTop: '14px' }}>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Your Quoted Price</span>
                        <span className={styles.metaValue} style={{ color: 'var(--tm-teal-800)', fontSize: '16px' }}>
                          ${proposal.proposedPrice}
                        </span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Submitted</span>
                        <span className={styles.metaValue}>
                          {new Date(proposal.createdAt).toLocaleDateString()}
                        </span>
                      </div>
                    </div>

                    <p className={styles.cardSnippet} style={{ marginTop: '12px' }}>
                      {proposal.message || proposal.inclusions || 'No pitch description provided.'}
                    </p>

                    {proposal.itineraryDayCount > 0 ? (
                      <span style={{ fontSize: '12px', color: 'var(--tm-slate-500)', display: 'block', marginTop: '6px' }}>
                        📅 {proposal.itineraryDayCount} itinerary day(s) planned
                      </span>
                    ) : null}
                  </div>

                  <div className={styles.cardFooter}>
                    <Link
                      className={styles.secondaryButton}
                      style={{ flex: 1 }}
                      to={`/guide/proposals/${proposal.id}`}
                    >
                      View Details
                    </Link>

                    {canWithdraw ? (
                      <button
                        className={styles.quietButton}
                        onClick={() => setWithdrawTarget(proposal)}
                        style={{ color: 'var(--tm-danger-600)' }}
                        type="button"
                      >
                        Withdraw
                      </button>
                    ) : null}
                  </div>
                </article>
              )
            })}
          </div>
        )}

        {/* Withdraw Confirmation Dialog */}
        <ConfirmDialog
          busy={isWithdrawing}
          confirmLabel="Withdraw Proposal"
          description={`Are you sure you want to withdraw your proposal for "${withdrawTarget?.tripRequestDestination}"? The tourist will no longer be able to accept it.`}
          onCancel={() => setWithdrawTarget(null)}
          onConfirm={handleConfirmWithdraw}
          open={Boolean(withdrawTarget)}
          title="Withdraw Proposal?"
          tone="danger"
        />
      </div>
    </DashboardLayout>
  )
}

export default GuideProposalsPage

