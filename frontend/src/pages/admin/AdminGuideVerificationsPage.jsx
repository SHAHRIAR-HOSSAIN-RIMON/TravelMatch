import { useCallback, useEffect, useRef, useState } from 'react'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import ConfirmDialog from '../../components/tourist/ConfirmDialog.jsx'
import GuideReviewsSection from '../../components/reviews/GuideReviewsSection.jsx'
import { useAuth } from '../../context/useAuth.js'
import { getPendingGuideVerifications, setGuideVerificationStatus } from '../../lib/adminService.js'
import { describeApiError } from '../../lib/api.js'
import styles from '../../components/admin/Admin.module.css'

export function AdminGuideVerificationsPage() {
  const { user, token, logout } = useAuth()

  const [guides, setGuides] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [feedback, setFeedback] = useState(null)

  // Pagination
  const [page, setPage] = useState(1)
  const [pageSize] = useState(10)
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)

  // Verification modal state
  const [verifyingGuide, setVerifyingGuide] = useState(null)
  const [rejectingGuide, setRejectingGuide] = useState(null)
  const [rejectionNote, setRejectionNote] = useState('')
  const [inspectingGuideId, setInspectingGuideId] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const requestIdRef = useRef(0)

  const fetchPendingGuides = useCallback(
    async (currentPage = page) => {
      if (!token) return

      const requestId = ++requestIdRef.current
      setLoading(true)
      setError(null)

      try {
        const result = await getPendingGuideVerifications(token, {
          page: currentPage,
          pageSize,
        })

        if (requestId !== requestIdRef.current) return

        setGuides(result.data || [])
        setTotalCount(result.totalCount || 0)
        setTotalPages(result.totalPages || 0)
        setPage(result.page || currentPage)
      } catch (err) {
        if (requestId !== requestIdRef.current) return
        setError(describeApiError(err, 'Failed to load pending guide verification requests.'))
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false)
        }
      }
    },
    [page, pageSize, token],
  )

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchPendingGuides(1)
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchPendingGuides])

  async function handleApproveGuide() {
    if (!token || !verifyingGuide) return

    setIsSubmitting(true)
    setFeedback(null)

    try {
      await setGuideVerificationStatus(token, verifyingGuide.userId, {
        status: 'Verified',
      })

      setFeedback({
        tone: 'success',
        message: `Guide ${verifyingGuide.fullName || verifyingGuide.email} is now verified!`,
      })
      setVerifyingGuide(null)
      fetchPendingGuides(page)
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: describeApiError(err, 'Failed to verify guide.'),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleRejectGuide() {
    if (!token || !rejectingGuide) return

    const trimmed = rejectionNote.trim()
    if (!trimmed) {
      setFeedback({
        tone: 'error',
        message: 'A rejection note explaining the reason is required.',
      })
      return
    }

    setIsSubmitting(true)
    setFeedback(null)

    try {
      await setGuideVerificationStatus(token, rejectingGuide.userId, {
        status: 'Rejected',
        note: trimmed,
      })

      setFeedback({
        tone: 'success',
        message: `Guide verification for ${rejectingGuide.fullName || rejectingGuide.email} was rejected.`,
      })
      setRejectingGuide(null)
      setRejectionNote('')
      fetchPendingGuides(page)
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: describeApiError(err, 'Failed to reject guide verification.'),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.header}>
          <h1 className={styles.title}>Guide Verification Queue</h1>
          <p className={styles.subtitle}>
            Review credentials, experience, and service areas for guides awaiting platform certification.
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

        <div style={{ marginBottom: 20, fontSize: 14, color: 'var(--tm-slate-600)' }}>
          Pending review: <strong>{totalCount}</strong> guide{totalCount === 1 ? '' : 's'}
        </div>

        {loading ? (
          <div style={{ padding: 48, textAlign: 'center', color: 'var(--tm-slate-500)' }}>
            Loading verification queue…
          </div>
        ) : guides.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>No pending verification requests</h3>
            <p className={styles.emptyText}>
              All submitted guide verification requests have been reviewed and processed.
            </p>
          </div>
        ) : (
          <div className={styles.verificationGrid}>
            {guides.map((guide) => {
              const isInspecting = inspectingGuideId === guide.guideProfileId

              return (
                <article className={styles.verificationCard} key={guide.userId}>
                  <div className={styles.verificationHeader}>
                    <div>
                      <h2 className={styles.guideName}>{guide.fullName || 'Anonymous Guide'}</h2>
                      <span className={styles.guideEmail}>{guide.email}</span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span
                        style={{
                          padding: '4px 10px',
                          borderRadius: 'var(--tm-radius-pill)',
                          background: '#fef3c7',
                          color: '#92400e',
                          fontSize: 12,
                          fontWeight: 700,
                        }}
                      >
                        Pending Verification
                      </span>
                    </div>
                  </div>

                  <div className={styles.guideDetailsGrid}>
                    <div>
                      <span style={{ fontSize: 12, color: 'var(--tm-slate-500)', fontWeight: 600 }}>
                        SERVICE AREA
                      </span>
                      <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--tm-slate-900)' }}>
                        {guide.serviceArea || 'Not specified'}
                      </div>
                    </div>

                    <div>
                      <span style={{ fontSize: 12, color: 'var(--tm-slate-500)', fontWeight: 600 }}>
                        EXPERIENCE
                      </span>
                      <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--tm-slate-900)' }}>
                        {guide.experienceYears > 0
                          ? `${guide.experienceYears} year${guide.experienceYears === 1 ? '' : 's'}`
                          : 'Not reported'}
                      </div>
                    </div>

                    <div>
                      <span style={{ fontSize: 12, color: 'var(--tm-slate-500)', fontWeight: 600 }}>
                        SUPPORTED TRIP TYPES
                      </span>
                      <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--tm-slate-900)' }}>
                        {guide.supportedTripTypes || 'All trip types'}
                      </div>
                    </div>

                    <div>
                      <span style={{ fontSize: 12, color: 'var(--tm-slate-500)', fontWeight: 600 }}>
                        USER & PROFILE IDs
                      </span>
                      <div style={{ fontSize: 13, color: 'var(--tm-slate-700)' }}>
                        User #{guide.userId} · Profile #{guide.guideProfileId}
                      </div>
                    </div>
                  </div>

                  {guide.bio && (
                    <div className={styles.guideBio}>
                      <strong>Bio & Background:</strong> {guide.bio}
                    </div>
                  )}

                  {/* Reviews drawer toggle */}
                  <div style={{ marginBottom: 16 }}>
                    <button
                      className={styles.actionBtn}
                      onClick={() =>
                        setInspectingGuideId(isInspecting ? null : guide.guideProfileId)
                      }
                      type="button"
                    >
                      {isInspecting ? '▲ Hide Past Reviews' : '▼ Check Past Reviews & Rating'}
                    </button>
                  </div>

                  {isInspecting && (
                    <div style={{ marginBottom: 20 }}>
                      <GuideReviewsSection
                        guideProfileId={guide.guideProfileId}
                        title={`Reviews for ${guide.fullName}`}
                        token={token}
                      />
                    </div>
                  )}

                  <div className={styles.verificationActions}>
                    <button
                      className={`${styles.actionBtn} ${styles.actionBtnDanger}`}
                      onClick={() => {
                        setRejectingGuide(guide)
                        setRejectionNote('')
                      }}
                      type="button"
                    >
                      Reject Application…
                    </button>

                    <button
                      className={`${styles.actionBtn} ${styles.actionBtnSuccess}`}
                      onClick={() => setVerifyingGuide(guide)}
                      type="button"
                    >
                      ✓ Approve & Verify Guide
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className={styles.pagination}>
            <button
              className={styles.pageButton}
              disabled={page <= 1 || loading}
              onClick={() => fetchPendingGuides(page - 1)}
              type="button"
            >
              ← Previous
            </button>
            <span className={styles.pageInfo}>
              Page {page} of {totalPages}
            </span>
            <button
              className={styles.pageButton}
              disabled={page >= totalPages || loading}
              onClick={() => fetchPendingGuides(page + 1)}
              type="button"
            >
              Next →
            </button>
          </div>
        )}

        {/* Approve Confirmation Dialog */}
        <ConfirmDialog
          busy={isSubmitting}
          confirmLabel="Verify Guide"
          description={`Are you sure you want to certify ${verifyingGuide?.fullName || verifyingGuide?.email}? They will receive the verified guide badge and be eligible to submit proposals and lead trips.`}
          onCancel={() => setVerifyingGuide(null)}
          onConfirm={handleApproveGuide}
          open={Boolean(verifyingGuide)}
          title="Approve Guide Verification"
          tone="primary"
        />

        {/* Reject Dialog with Rejection Note */}
        {rejectingGuide && (
          <div
            aria-modal="true"
            className={styles.modalOverlay}
            onClick={(e) => {
              if (e.target === e.currentTarget && !isSubmitting) setRejectingGuide(null)
            }}
            role="dialog"
          >
            <div className={styles.modalContent}>
              <h2 className={styles.modalTitle}>Reject Verification Request</h2>
              <p className={styles.modalSubtitle}>
                Rejecting application for {rejectingGuide.fullName || rejectingGuide.email}.
              </p>

              <div className={styles.formGroup}>
                <label className={styles.label} htmlFor="rejection-note">
                  Reason for Rejection <span style={{ color: 'var(--tm-danger-600)' }}>*</span>
                </label>
                <textarea
                  className={styles.textarea}
                  id="rejection-note"
                  maxLength={1000}
                  onChange={(e) => setRejectionNote(e.target.value)}
                  placeholder="Please state why this application is rejected (e.g. invalid documentation, insufficient experience in stated area, identity mismatch)."
                  required
                  rows={4}
                  value={rejectionNote}
                />
                <span style={{ fontSize: 12, color: 'var(--tm-slate-400)', display: 'block', textAlign: 'right', marginTop: 4 }}>
                  {rejectionNote.length} / 1000 characters
                </span>
              </div>

              <div className={styles.modalActions}>
                <button
                  className={styles.actionBtn}
                  disabled={isSubmitting}
                  onClick={() => setRejectingGuide(null)}
                  type="button"
                >
                  Cancel
                </button>
                <button
                  className={`${styles.actionBtn} ${styles.actionBtnDanger}`}
                  disabled={isSubmitting || !rejectionNote.trim()}
                  onClick={handleRejectGuide}
                  type="button"
                >
                  {isSubmitting ? 'Rejecting…' : 'Confirm Rejection'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </DashboardLayout>
  )
}

export default AdminGuideVerificationsPage

