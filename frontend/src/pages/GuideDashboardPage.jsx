import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import VerificationBadge from '../components/guide/VerificationBadge.jsx'
import ProposalStatusPill from '../components/guide/ProposalStatusPill.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError } from '../lib/api.js'
import { getOpenTripRequests, getMyProposals } from '../lib/guideService.js'
import { getProfile } from '../lib/profileService.js'
import GuideReviewsSection from '../components/reviews/GuideReviewsSection.jsx'
import styles from '../components/guide/Guide.module.css'

const LOADING = 'loading'
const READY = 'ready'
const ERROR = 'error'

export function GuideDashboardPage() {
  const { user, token, logout } = useAuth()

  const [dashboardState, setDashboardState] = useState({
    status: LOADING,
    profile: null,
    openRequests: [],
    totalOpenCount: 0,
    canSubmitProposal: false,
    verificationStatus: 'Pending',
    myProposals: [],
    error: null,
  })

  const loadData = useCallback(async () => {
    if (!token) return

    setDashboardState((prev) => ({ ...prev, status: LOADING, error: null }))

    try {
      const [profileResult, openRequestsResult, proposalsResult] = await Promise.allSettled([
        getProfile(token),
        getOpenTripRequests(token, { page: 1, pageSize: 4 }),
        getMyProposals(token),
      ])

      if (
        (profileResult.status === 'rejected' && profileResult.reason instanceof ApiError && profileResult.reason.status === 401) ||
        (openRequestsResult.status === 'rejected' && openRequestsResult.reason instanceof ApiError && openRequestsResult.reason.status === 401)
      ) {
        logout()
        return
      }

      const profile = profileResult.status === 'fulfilled' ? profileResult.value : null
      const openReq = openRequestsResult.status === 'fulfilled' ? openRequestsResult.value : null
      const proposals = proposalsResult.status === 'fulfilled' ? proposalsResult.value : []

      const vStatus =
        openReq?.guideVerificationStatus ??
        profile?.guideProfile?.verificationStatus ??
        'Pending'

      const canSubmit = openReq?.canSubmitProposal ?? (vStatus === 'Verified')

      setDashboardState({
        status: READY,
        profile,
        openRequests: openReq?.data ?? [],
        totalOpenCount: openReq?.totalOpenCount ?? openReq?.totalCount ?? 0,
        canSubmitProposal: canSubmit,
        verificationStatus: vStatus,
        myProposals: proposals,
        error: null,
      })
    } catch (err) {
      setDashboardState((prev) => ({
        ...prev,
        status: ERROR,
        error: err instanceof Error ? err.message : 'Could not load dashboard information.',
      }))
    }
  }, [logout, token])

  useEffect(() => {
    loadData()
  }, [loadData])

  // Derive counts from actual data
  const metrics = useMemo(() => {
    const proposals = dashboardState.myProposals || []
    const submittedCount = proposals.filter((p) => p.status === 'Submitted').length
    const acceptedCount = proposals.filter((p) => p.status === 'Accepted').length
    const draftCount = proposals.filter((p) => p.status === 'Draft').length

    return {
      totalOpenRequests: dashboardState.totalOpenCount,
      totalProposals: proposals.length,
      submittedProposals: submittedCount,
      acceptedProposals: acceptedCount,
      draftProposals: draftCount,
    }
  }, [dashboardState.myProposals, dashboardState.totalOpenCount])

  const acceptedTrips = useMemo(() => {
    return (dashboardState.myProposals || []).filter((p) => p.status === 'Accepted').slice(0, 3)
  }, [dashboardState.myProposals])

  const recentProposals = useMemo(() => {
    return (dashboardState.myProposals || []).slice(0, 4)
  }, [dashboardState.myProposals])

  const guideProfile = dashboardState.profile?.guideProfile

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        {/* Welcome Section */}
        <section className={styles.welcomeCard} aria-labelledby="guide-welcome-heading">
          <div className={styles.welcomeText}>
            <h2 id="guide-welcome-heading">Welcome, {user?.fullName || 'Guide'}!</h2>
            <p>
              {dashboardState.verificationStatus === 'Verified'
                ? 'Your guide profile is verified and active. You are eligible to submit competitive proposals for open traveler requests.'
                : 'Your guide profile is currently pending verification. You can explore traveler requests now; proposal bidding will be unlocked upon approval.'}
            </p>
          </div>

          <div className={styles.welcomeAside}>
            <VerificationBadge showDetail={false} status={dashboardState.verificationStatus} />
            <Link className={styles.secondaryButton} to="/profile" style={{ fontSize: '13px', minHeight: '34px', padding: '0 14px' }}>
              Profile Settings
            </Link>
          </div>
        </section>

        {/* Verification Status Warning if not verified */}
        {dashboardState.status === READY && !dashboardState.canSubmitProposal ? (
          <div className={`${styles.noticeBanner} ${styles.noticeWarning}`} role="status">
            <span className={styles.noticeIcon} aria-hidden="true">⚠️</span>
            <div>
              <strong>Account Under Verification:</strong> You are currently able to browse open trip requests, but proposal bidding requires administrator verification. Ensure your bio, experience, and service areas are up to date in your <Link to="/profile" style={{ textDecoration: 'underline', fontWeight: 600 }}>Profile</Link>.
            </div>
          </div>
        ) : null}

        {/* Error State */}
        {dashboardState.status === ERROR ? (
          <div className={`${styles.noticeBanner} ${styles.noticeError}`} role="alert">
            <span className={styles.noticeIcon} aria-hidden="true">!</span>
            <div style={{ flex: 1 }}>
              <strong>Could not load dashboard data:</strong> {dashboardState.error}
            </div>
            <button className={styles.secondaryButton} onClick={loadData} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {/* Stats Metrics Grid */}
        <section className={styles.statsGrid} aria-label="Key metrics">
          <div className={styles.statCard}>
            <span className={styles.statLabel}>Available Requests</span>
            <span className={styles.statValue}>
              {dashboardState.status === LOADING ? '…' : metrics.totalOpenRequests}
            </span>
            <span className={styles.statSubtext}>Open for bidding</span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>My Proposals</span>
            <span className={styles.statValue}>
              {dashboardState.status === LOADING ? '…' : metrics.totalProposals}
            </span>
            <span className={styles.statSubtext}>
              {metrics.submittedProposals} submitted, {metrics.draftProposals} drafts
            </span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>Accepted Trips</span>
            <span className={styles.statValue}>
              {dashboardState.status === LOADING ? '…' : metrics.acceptedProposals}
            </span>
            <span className={styles.statSubtext}>Active & confirmed</span>
          </div>

          <div className={styles.statCard}>
            <span className={styles.statLabel}>Guide Rating</span>
            <span className={styles.statValue}>
              {dashboardState.status === LOADING
                ? '…'
                : guideProfile?.averageRating != null
                ? `${Number(guideProfile.averageRating).toFixed(1)} ★`
                : '—'}
            </span>
            <span className={styles.statSubtext}>
              {guideProfile?.experienceYears ? `${guideProfile.experienceYears} yrs experience` : 'Guide profile'}
            </span>
          </div>
        </section>

        {/* Quick Actions Band */}
        <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', marginBottom: '32px' }}>
          <Link className={styles.primaryButton} to="/guide/trip-requests">
            🔍 Browse Open Requests
          </Link>
          <Link className={styles.secondaryButton} to="/guide/proposals">
            📋 View My Proposals ({metrics.totalProposals})
          </Link>
          <Link className={styles.secondaryButton} to="/guide/trips">
            🎒 Assigned Trips ({metrics.acceptedProposals})
          </Link>
          <Link className={styles.secondaryButton} to="/profile">
            👤 Update Profile
          </Link>
        </div>

        {/* Accepted Trips / Assignments Preview */}
        {acceptedTrips.length > 0 ? (
          <section style={{ marginBottom: '36px' }}>
            <div className={styles.sectionHeader}>
              <h3 className={styles.sectionTitle}>Confirmed & Accepted Trips</h3>
              <Link className={styles.viewAllLink} to="/guide/trips">
                View all assigned trips →
              </Link>
            </div>

            <div className={styles.cardGrid}>
              {acceptedTrips.map((proposal) => (
                <article className={styles.tripCard} key={proposal.id}>
                  <div>
                    <div className={styles.cardTop}>
                      <div>
                        <h4 className={styles.cardDestination}>{proposal.tripRequestDestination}</h4>
                        <span className={styles.tripTypeTag}>Accepted Proposal</span>
                      </div>
                      <ProposalStatusPill status="Accepted" />
                    </div>

                    <div className={styles.cardMetaGrid} style={{ marginTop: '14px' }}>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Trip Dates</span>
                        <span className={styles.metaValue}>
                          {proposal.tripRequestStartDate} to {proposal.tripRequestEndDate}
                        </span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Agreed Price</span>
                        <span className={styles.metaValue}>${proposal.proposedPrice}</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.cardFooter}>
                    <Link className={styles.secondaryButton} to={`/guide/proposals/${proposal.id}`} style={{ width: '100%' }}>
                      Manage Trip Details & Itinerary
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          </section>
        ) : null}

        {/* Open Requests Preview */}
        <section style={{ marginBottom: '36px' }}>
          <div className={styles.sectionHeader}>
            <h3 className={styles.sectionTitle}>Recent Traveler Trip Requests</h3>
            <Link className={styles.viewAllLink} to="/guide/trip-requests">
              Browse all open requests →
            </Link>
          </div>

          {dashboardState.status === LOADING ? (
            <div className={styles.cardGrid}>
              <div className={styles.skeletonCard} />
              <div className={styles.skeletonCard} />
            </div>
          ) : dashboardState.openRequests.length === 0 ? (
            <div className={styles.emptyState}>
              <h4 className={styles.emptyTitle}>No open trip requests at this time</h4>
              <p className={styles.emptyText}>
                When tourists post custom travel requests, they will show up here for you to review and bid on.
              </p>
            </div>
          ) : (
            <div className={styles.cardGrid}>
              {dashboardState.openRequests.map((request) => (
                <article className={styles.requestCard} key={request.id}>
                  <div>
                    <div className={styles.cardTop}>
                      <div>
                        <h4 className={styles.cardDestination}>{request.destination}</h4>
                        <span className={styles.tripTypeTag}>{request.tripType || 'Custom Tour'}</span>
                      </div>
                      <span className={styles.statusPill} style={{ background: '#f0fdf4', color: '#166534' }}>
                        Open for bidding
                      </span>
                    </div>

                    <div className={styles.cardMetaGrid} style={{ marginTop: '14px' }}>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Dates</span>
                        <span className={styles.metaValue}>{request.startDate} – {request.endDate}</span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Travelers</span>
                        <span className={styles.metaValue}>{request.numberOfTravelers} person(s)</span>
                      </div>
                    </div>

                    <p className={styles.cardSnippet} style={{ marginTop: '12px' }}>
                      {request.description || request.travelPreferences || 'No specific description provided.'}
                    </p>
                  </div>

                  <div className={styles.cardFooter}>
                    <div>
                      <span className={styles.cardPriceLabel}>Budget</span>
                      <span className={styles.cardPriceHighlight}>
                        {request.budgetMin && request.budgetMax
                          ? `$${request.budgetMin} – $${request.budgetMax}`
                          : `$${request.budget}`}
                      </span>
                    </div>

                    <Link className={styles.primaryButton} to={`/guide/trip-requests/${request.id}`}>
                      {dashboardState.canSubmitProposal ? 'View & Bid' : 'View Details'}
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        {/* My Submitted Proposals Preview */}
        <section>
          <div className={styles.sectionHeader}>
            <h3 className={styles.sectionTitle}>My Proposals</h3>
            <Link className={styles.viewAllLink} to="/guide/proposals">
              View all proposals ({metrics.totalProposals}) →
            </Link>
          </div>

          {dashboardState.status === LOADING ? (
            <div className={styles.cardGrid}>
              <div className={styles.skeletonCard} />
            </div>
          ) : recentProposals.length === 0 ? (
            <div className={styles.emptyState}>
              <h4 className={styles.emptyTitle}>You haven&apos;t submitted any proposals yet</h4>
              <p className={styles.emptyText}>
                Browse available trip requests and send your custom offer to tourists looking for local guides.
              </p>
              <Link className={styles.primaryButton} to="/guide/trip-requests">
                Browse Trip Requests
              </Link>
            </div>
          ) : (
            <div className={styles.cardGrid}>
              {recentProposals.map((proposal) => (
                <article className={styles.proposalCard} key={proposal.id}>
                  <div>
                    <div className={styles.cardTop}>
                      <div>
                        <h4 className={styles.cardDestination}>{proposal.tripRequestDestination || `Trip Request #${proposal.tripRequestId}`}</h4>
                        <span className={styles.metaLabel}>
                          {proposal.tripRequestStartDate} to {proposal.tripRequestEndDate}
                        </span>
                      </div>
                      <ProposalStatusPill status={proposal.status} />
                    </div>

                    <p className={styles.cardSnippet} style={{ marginTop: '14px' }}>
                      {proposal.message || proposal.inclusions || 'No pitch text.'}
                    </p>
                  </div>

                  <div className={styles.cardFooter}>
                    <div>
                      <span className={styles.cardPriceLabel}>Quoted Price</span>
                      <span className={styles.cardPriceHighlight}>${proposal.proposedPrice}</span>
                    </div>

                    <Link className={styles.secondaryButton} to={`/guide/proposals/${proposal.id}`}>
                      View Details
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        {/* Client Reviews Section (Phase 7) */}
        {guideProfile?.id && (
          <GuideReviewsSection
            guideProfileId={guideProfile.id}
            title="Recent Client Reviews"
            token={token}
          />
        )}
      </div>
    </DashboardLayout>
  )
}

export default GuideDashboardPage

