import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import ProposalForm from '../components/guide/ProposalForm.jsx'
import VerificationBadge from '../components/guide/VerificationBadge.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from '../lib/api.js'
import {
  getOpenTripRequestDetail,
  createAndSubmitProposal,
  getMyProposals,
} from '../lib/guideService.js'
import styles from '../components/guide/Guide.module.css'

export function GuideTripRequestDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { user, token, logout } = useAuth()

  const [loading, setLoading] = useState(true)
  const [request, setRequest] = useState(null)
  const [canSubmit, setCanSubmit] = useState(false)
  const [verificationStatus, setVerificationStatus] = useState('Pending')
  const [existingProposal, setExistingProposal] = useState(null)
  const [error, setError] = useState(null)
  const [feedback, setFeedback] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const tripRequestId = Number(id)

  const requestIdRef = useRef(0)

  const loadDetail = useCallback(async () => {
    if (!token || !tripRequestId) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const [detailRes, proposalsRes] = await Promise.allSettled([
        getOpenTripRequestDetail(token, tripRequestId),
        getMyProposals(token),
      ])

      if (requestId !== requestIdRef.current) return

      if (
        detailRes.status === 'rejected' &&
        detailRes.reason instanceof ApiError &&
        detailRes.reason.status === 401
      ) {
        logout()
        return
      }

      if (detailRes.status === 'fulfilled') {
        const detail = detailRes.value
        setRequest(detail.data)
        setCanSubmit(detail.canSubmitProposal)
        setVerificationStatus(detail.guideVerificationStatus)
      } else {
        const reason = detailRes.reason
        setError(
          reason instanceof ApiError
            ? describeApiError(reason)
            : 'Could not load trip request details.',
        )
      }

      if (proposalsRes.status === 'fulfilled') {
        const myProposals = proposalsRes.value || []
        const match = myProposals.find((p) => p.tripRequestId === tripRequestId)
        setExistingProposal(match || null)
      }
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      setError(err instanceof Error ? err.message : 'An unexpected error occurred.')
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [logout, token, tripRequestId])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await loadDetail()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [loadDetail])

  async function handleProposalSubmit(formValues) {
    if (!token || !tripRequestId || isSubmitting) return

    setIsSubmitting(true)
    setFeedback(null)

    try {
      const submitted = await createAndSubmitProposal(token, tripRequestId, formValues)
      setFeedback({
        tone: 'success',
        message: 'Your proposal was submitted successfully! The tourist has been notified.',
      })

      const proposalId = submitted?.id ?? submitted?.Id

      // After a short delay, navigate to the submitted proposal
      if (proposalId) {
        setTimeout(() => {
          navigate(`/guide/proposals/${proposalId}`)
        }, 1200)
      } else {
        loadDetail()
      }
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 403) {
          setFeedback({
            tone: 'error',
            message: 'Permission denied: Only verified guides can submit proposals.',
          })
        } else if (err.status === 409) {
          setFeedback({
            tone: 'error',
            message: 'A proposal for this trip request has already been submitted.',
          })
        } else {
          setFeedback({
            tone: 'error',
            message: describeApiError(err, 'Failed to submit proposal.'),
          })
        }
      } else {
        setFeedback({
          tone: 'error',
          message: err instanceof Error ? err.message : 'Submission failed. Please try again.',
        })
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backTo="/guide/trip-requests"
          backLabel="Browse Trip Requests"
          eyebrow="Trip Request Details"
          title={request?.destination || 'Trip Request Details'}
          description="Review the traveler's itinerary requirements, duration, and preferences before submitting your offer."
        />

        {/* Feedback Banner */}
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
            <button className={styles.secondaryButton} onClick={loadDetail} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {loading ? (
          <div className={styles.detailGrid}>
            <div className={styles.skeletonCard} style={{ height: '400px' }} />
            <div className={styles.skeletonCard} style={{ height: '400px' }} />
          </div>
        ) : request ? (
          <div className={styles.detailGrid}>
            {/* Main Request Information */}
            <div className={styles.detailMainCard}>
              <div className={styles.detailHeader}>
                <div>
                  <h2 style={{ margin: 0, fontSize: '24px', fontWeight: 700, color: 'var(--tm-slate-900)' }}>
                    {request.destination}
                  </h2>
                  <span className={styles.tripTypeTag} style={{ marginTop: '6px' }}>
                    {request.tripType || 'Custom Tour'}
                  </span>
                </div>
                <span className={styles.statusPill} style={{ background: '#f0fdf4', color: '#166534' }}>
                  {request.status}
                </span>
              </div>

              {/* Meta items */}
              <div className={styles.detailMetaList}>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Start Date</span>
                  <span className={styles.metaValue}>{request.startDate}</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>End Date</span>
                  <span className={styles.metaValue}>{request.endDate}</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Duration</span>
                  <span className={styles.metaValue}>{request.numberOfDays} Day(s)</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Travelers</span>
                  <span className={styles.metaValue}>{request.numberOfTravelers} Person(s)</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Budget Range</span>
                  <span className={styles.metaValue} style={{ color: 'var(--tm-teal-800)' }}>
                    {request.budgetMin && request.budgetMax
                      ? `$${request.budgetMin} – $${request.budgetMax}`
                      : `$${request.budget}`}
                  </span>
                </div>
              </div>

              {/* Trip Description */}
              <div className={styles.contentBlock}>
                <h3>Trip Description & Goals</h3>
                <p>{request.description || 'No detailed description provided by the traveler.'}</p>
              </div>

              {/* Travel Preferences */}
              {request.travelPreferences ? (
                <div className={styles.contentBlock}>
                  <h3>Special Preferences & Requirements</h3>
                  <p>{request.travelPreferences}</p>
                </div>
              ) : null}

              {/* Verification & Guide Role notice */}
              <div style={{ marginTop: '28px', paddingTop: '20px', borderTop: '1px solid var(--tm-border-soft)' }}>
                <span className={styles.metaLabel}>Your Guide Account Status</span>
                <div style={{ marginTop: '6px' }}>
                  <VerificationBadge showDetail={true} status={verificationStatus} />
                </div>
              </div>
            </div>

            {/* Proposal Submission or Existing Proposal Section */}
            <div className={styles.detailSideCard}>
              {existingProposal ? (
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                    <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>Your Submitted Proposal</h3>
                    <span className={styles.statusPill} style={{ background: '#ecfdf5', color: '#047857' }}>
                      {existingProposal.status}
                    </span>
                  </div>

                  <p style={{ fontSize: '14px', color: 'var(--tm-slate-600)', lineHeight: 1.5, marginBottom: '18px' }}>
                    You have already submitted a proposal for this trip request on{' '}
                    {new Date(existingProposal.createdAt).toLocaleDateString()}.
                  </p>

                  <div className={styles.detailMetaList} style={{ gridTemplateColumns: '1fr', marginBottom: '20px' }}>
                    <div className={styles.metaItem}>
                      <span className={styles.metaLabel}>Your Quoted Price</span>
                      <span className={styles.metaValue} style={{ fontSize: '20px', color: 'var(--tm-teal-800)' }}>
                        ${existingProposal.proposedPrice}
                      </span>
                    </div>
                    {existingProposal.availability ? (
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Availability</span>
                        <span className={styles.metaValue}>{existingProposal.availability}</span>
                      </div>
                    ) : null}
                  </div>

                  <Link
                    className={styles.primaryButton}
                    style={{ width: '100%' }}
                    to={`/guide/proposals/${existingProposal.id}`}
                  >
                    View / Manage Proposal →
                  </Link>
                </div>
              ) : !canSubmit ? (
                <div>
                  <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', fontWeight: 700 }}>
                    Bidding Unavailable
                  </h3>
                  <div className={`${styles.noticeBanner} ${styles.noticeWarning}`} style={{ margin: '0 0 16px 0' }}>
                    <span className={styles.noticeIcon} aria-hidden="true">⚠️</span>
                    <div>
                      <strong>Account Verification Required:</strong> Only verified guides can submit proposals. Your guide account is currently <strong>{verificationStatus}</strong>.
                    </div>
                  </div>
                  <p style={{ fontSize: '14px', color: 'var(--tm-slate-600)', lineHeight: 1.5 }}>
                    Please ensure your experience, certifications, and service area details are complete on your profile page to speed up administrator review.
                  </p>
                  <Link className={styles.secondaryButton} style={{ width: '100%', marginTop: '12px' }} to="/profile">
                    Go to Profile
                  </Link>
                </div>
              ) : (
                <div>
                  <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', fontWeight: 700 }}>
                    Submit a Proposal
                  </h3>
                  <p style={{ fontSize: '13px', color: 'var(--tm-slate-500)', lineHeight: 1.5, margin: '0 0 20px 0' }}>
                    Quote your fee and outline what you will provide. The tourist will review your proposal and choose a guide.
                  </p>

                  <ProposalForm
                    isSubmitting={isSubmitting}
                    onSubmit={handleProposalSubmit}
                    tripRequest={request}
                  />
                </div>
              )}
            </div>
          </div>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default GuideTripRequestDetailPage

