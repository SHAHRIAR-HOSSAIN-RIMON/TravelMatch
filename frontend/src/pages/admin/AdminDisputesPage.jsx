import { useCallback, useEffect, useRef, useState } from 'react'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import DisputeStatusPill from '../../components/disputes/DisputeStatusPill.jsx'
import DisputeDetailModal from '../../components/disputes/DisputeDetailModal.jsx'
import ResolveDisputeModal from '../../components/disputes/ResolveDisputeModal.jsx'
import { useAuth } from '../../context/useAuth.js'
import { getDisputes } from '../../lib/disputeService.js'
import { describeApiError } from '../../lib/api.js'
import styles from '../../components/admin/Admin.module.css'

function formatDate(isoString) {
  if (!isoString) return '—'
  try {
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(isoString))
  } catch {
    return String(isoString)
  }
}

export function AdminDisputesPage() {
  const { user, token, logout } = useAuth()

  const [disputes, setDisputes] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [feedback, setFeedback] = useState(null)

  // Filter
  const [statusFilter, setStatusFilter] = useState('All')

  // Modals state
  const [activeDetailDispute, setActiveDetailDispute] = useState(null)
  const [activeResolvingDispute, setActiveResolvingDispute] = useState(null)

  const requestIdRef = useRef(0)

  const fetchDisputes = useCallback(async () => {
    if (!token) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const result = await getDisputes(token, statusFilter)
      if (requestId !== requestIdRef.current) return
      setDisputes(result.data || [])
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      setError(describeApiError(err, 'Failed to load disputes queue.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [statusFilter, token])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchDisputes()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchDisputes])

  function handleResolutionSuccess(updatedResult) {
    setFeedback({
      tone: 'success',
      message: `Dispute #${updatedResult?.data?.id ?? ''} has been resolved successfully. Escrow funds have been unblocked.`,
    })
    fetchDisputes()
  }

  const openCount = disputes.filter((d) => d.status === 'Open').length

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.header}>
          <h1 className={styles.title}>Dispute Management</h1>
          <p className={styles.subtitle}>
            Investigate reported disputes between tourists, guides, and organizers and record formal resolutions.
          </p>
        </div>

        {feedback && (
          <div style={{ marginBottom: 20 }}>
            <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner>
          </div>
        )}

        {error && (
          <div style={{ marginBottom: 20 }}>
            <FeedbackBanner tone="error">{error}</FeedbackBanner>
          </div>
        )}

        {/* Filter Controls */}
        <div className={styles.filterBar}>
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel} htmlFor="dispute-status-filter">
              Dispute Status:
            </label>
            <select
              className={styles.select}
              id="dispute-status-filter"
              onChange={(e) => setStatusFilter(e.target.value)}
              value={statusFilter}
            >
              <option value="All">All Statuses</option>
              <option value="Open">Open</option>
              <option value="Resolved">Resolved</option>
              <option value="Closed">Closed</option>
            </select>
          </div>

          <div style={{ marginLeft: 'auto', fontSize: 13, color: 'var(--tm-slate-500)' }}>
            Showing <strong>{disputes.length}</strong> dispute{disputes.length === 1 ? '' : 's'}
            {openCount > 0 && (
              <span style={{ marginLeft: 8, color: '#b91c1c', fontWeight: 600 }}>
                ({openCount} awaiting resolution)
              </span>
            )}
          </div>
        </div>

        {/* Disputes Table */}
        {loading ? (
          <div className={styles.tableWrap} style={{ padding: 32, textAlign: 'center', color: 'var(--tm-slate-500)' }}>
            Loading disputes…
          </div>
        ) : disputes.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>No disputes in this queue</h3>
            <p className={styles.emptyText}>
              There are currently no disputes matching the selected status filter.
            </p>
          </div>
        ) : (
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Dispute ID</th>
                  <th>Trip Association</th>
                  <th>Reported By</th>
                  <th>Reason / Summary</th>
                  <th>Status</th>
                  <th>Date Filed</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {disputes.map((dispute) => {
                  const isOpen = dispute.status === 'Open'
                  return (
                    <tr key={dispute.id}>
                      <td style={{ fontWeight: 700 }}>#{dispute.id}</td>
                      <td>
                        <span style={{ fontWeight: 600 }}>{dispute.tripType}</span> #{dispute.tripId}
                      </td>
                      <td>User #{dispute.reportedByUserId}</td>
                      <td style={{ maxWidth: 280, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {dispute.reason}
                      </td>
                      <td>
                        <DisputeStatusPill status={dispute.status} />
                      </td>
                      <td>{formatDate(dispute.createdAt)}</td>
                      <td>
                        <div style={{ display: 'flex', gap: 8 }}>
                          <button
                            className={styles.actionBtn}
                            onClick={() => setActiveDetailDispute(dispute)}
                            type="button"
                          >
                            Details
                          </button>
                          {isOpen && (
                            <button
                              className={`${styles.actionBtn} ${styles.actionBtnSuccess}`}
                              onClick={() => setActiveResolvingDispute(dispute)}
                              type="button"
                            >
                              Resolve…
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Dispute Details Modal */}
        <DisputeDetailModal
          dispute={activeDetailDispute}
          isOpen={Boolean(activeDetailDispute)}
          onClose={() => setActiveDetailDispute(null)}
          onResolveClick={(disp) => setActiveResolvingDispute(disp)}
        />

        {/* Resolve Dispute Modal */}
        <ResolveDisputeModal
          dispute={activeResolvingDispute}
          isOpen={Boolean(activeResolvingDispute)}
          onClose={() => setActiveResolvingDispute(null)}
          onSuccess={handleResolutionSuccess}
          token={token}
        />
      </div>
    </DashboardLayout>
  )
}

export default AdminDisputesPage

