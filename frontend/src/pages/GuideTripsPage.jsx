import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import ProposalStatusPill from '../components/guide/ProposalStatusPill.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError } from '../lib/api.js'
import RaiseDisputeModal from '../components/disputes/RaiseDisputeModal.jsx'
import {
  getMyProposals,
  getProposalCostBreakdown,
  getItineraryTracking,
  updateActivityStatus,
} from '../lib/guideService.js'
import styles from '../components/guide/Guide.module.css'

export function GuideTripsPage() {
  const { user, token, logout } = useAuth()

  const [loading, setLoading] = useState(true)
  const [acceptedTrips, setAcceptedTrips] = useState([])
  const [error, setError] = useState(null)
  const [breakdowns, setBreakdowns] = useState({})
  const [trackingData, setTrackingData] = useState({})
  const [updatingActivityId, setUpdatingActivityId] = useState(null)
  const [feedback, setFeedback] = useState(null)
  const [disputeTarget, setDisputeTarget] = useState(null)

  const requestIdRef = useRef(0)

  const fetchTrips = useCallback(async () => {
    if (!token) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const myProposals = await getMyProposals(token)
      if (requestId !== requestIdRef.current) return

      const accepted = (myProposals || []).filter((p) => p.status === 'Accepted')
      setAcceptedTrips(accepted)

      // Fetch cost breakdowns and tracking for accepted proposals
      const breakdownMap = {}
      const trackingMap = {}

      await Promise.all(
        accepted.map(async (trip) => {
          try {
            const cb = await getProposalCostBreakdown(token, trip.id)
            if (requestId !== requestIdRef.current) return
            if (cb) breakdownMap[trip.id] = cb
          } catch {
            // Cost breakdown might not exist if not yet calculated
          }

          try {
            const tracking = await getItineraryTracking(token, trip.id)
            if (requestId !== requestIdRef.current) return
            if (tracking && tracking.length > 0) {
              trackingMap[trip.id] = tracking
            }
          } catch {
            // Tracking might be empty
          }
        }),
      )

      if (requestId !== requestIdRef.current) return
      setBreakdowns(breakdownMap)
      setTrackingData(trackingMap)
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      if (err instanceof ApiError && err.status === 401) {
        logout()
        return
      }
      setError(err instanceof Error ? err.message : 'Failed to load assigned trips.')
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [logout, token])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchTrips()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchTrips])

  async function handleStatusChange(proposalId, activityId, newStatus) {
    if (!token || updatingActivityId) return

    setUpdatingActivityId(activityId)
    setFeedback(null)

    try {
      await updateActivityStatus(token, activityId, newStatus)
      setFeedback({
        tone: 'success',
        message: 'Activity status updated successfully.',
      })

      // Update state locally
      setTrackingData((prev) => {
        const currentDays = prev[proposalId] || []
        const updatedDays = currentDays.map((day) => ({
          ...day,
          activitiesList: (day.activitiesList || []).map((act) =>
            act.id === activityId ? { ...act, status: newStatus } : act,
          ),
        }))
        return { ...prev, [proposalId]: updatedDays }
      })
    } catch {
      setFeedback({
        tone: 'error',
        message: 'Could not update activity status. Please try again.',
      })
    } finally {
      setUpdatingActivityId(null)
    }
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backTo="/guide/dashboard"
          backLabel="Guide Dashboard"
          eyebrow="Guide Module"
          title="Accepted Proposals & Assigned Trips"
          description="Manage trips where travelers have accepted your proposal. View itinerary details, cost breakdowns, and update tour progress."
        />

        {/* Feedback banner */}
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

        {/* Error banner */}
        {error ? (
          <div className={`${styles.noticeBanner} ${styles.noticeError}`} role="alert">
            <span className={styles.noticeIcon} aria-hidden="true">!</span>
            <div style={{ flex: 1 }}>{error}</div>
            <button className={styles.secondaryButton} onClick={fetchTrips} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {loading ? (
          <div className={styles.cardGrid}>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </div>
        ) : acceptedTrips.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>No accepted trips yet</h3>
            <p className={styles.emptyText}>
              When a traveler accepts one of your submitted proposals, the confirmed trip and its itinerary will be tracked here.
            </p>
            <Link className={styles.primaryButton} to="/guide/trip-requests">
              Browse Open Trip Requests
            </Link>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '32px' }}>
            {acceptedTrips.map((trip) => {
              const breakdown = breakdowns[trip.id]
              const tracking = trackingData[trip.id]

              return (
                <article
                  className={styles.detailMainCard}
                  key={trip.id}
                  style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}
                >
                  <div className={styles.detailHeader} style={{ marginBottom: 0, paddingBottom: '16px' }}>
                    <div>
                      <h3 style={{ margin: '0 0 4px 0', fontSize: '22px', fontWeight: 700, color: 'var(--tm-slate-900)' }}>
                        {trip.tripRequestDestination || `Trip Request #${trip.tripRequestId}`}
                      </h3>
                      <span className={styles.metaLabel}>
                        Trip Dates: {trip.tripRequestStartDate} to {trip.tripRequestEndDate}
                      </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <ProposalStatusPill status="Accepted" />
                      <Link
                        className={styles.secondaryButton}
                        style={{ fontSize: '13px', minHeight: '34px', padding: '0 14px' }}
                        to={`/guide/proposals/${trip.id}`}
                      >
                        Proposal View
                      </Link>
                      <button
                        className={styles.secondaryButton}
                        onClick={() => setDisputeTarget(trip)}
                        style={{ fontSize: '13px', minHeight: '34px', padding: '0 12px', color: '#b91c1c', borderColor: '#fca5a5' }}
                        type="button"
                      >
                        Raise Dispute
                      </button>
                    </div>
                  </div>

                  <div className={styles.detailMetaList}>
                    <div className={styles.metaItem}>
                      <span className={styles.metaLabel}>Agreed Price</span>
                      <span className={styles.metaValue} style={{ fontSize: '20px', color: 'var(--tm-teal-800)' }}>
                        ${trip.proposedPrice}
                      </span>
                    </div>
                    <div className={styles.metaItem}>
                      <span className={styles.metaLabel}>Related Request</span>
                      <Link
                        to={`/guide/trip-requests/${trip.tripRequestId}`}
                        style={{ color: 'var(--tm-teal-700)', fontWeight: 600, fontSize: '14px', textDecoration: 'underline' }}
                      >
                        Trip Request #{trip.tripRequestId}
                      </Link>
                    </div>
                    <div className={styles.metaItem}>
                      <span className={styles.metaLabel}>Accepted On</span>
                      <span className={styles.metaValue}>
                        {new Date(trip.updatedAt || trip.createdAt).toLocaleDateString()}
                      </span>
                    </div>
                    <div className={styles.metaItem}>
                      <span className={styles.metaLabel}>Availability Confirmed</span>
                      <span className={styles.metaValue}>{trip.availability || 'Confirmed'}</span>
                    </div>
                  </div>

                  {/* Pitch and terms */}
                  <div className={styles.contentBlock} style={{ marginBottom: 0 }}>
                    <h4 style={{ margin: '0 0 6px 0', fontSize: '15px', color: 'var(--tm-slate-800)' }}>
                      Inclusions & Terms
                    </h4>
                    <p style={{ fontSize: '14px', color: 'var(--tm-slate-600)' }}>
                      {trip.inclusions || 'Standard tour guiding inclusions.'}
                    </p>
                    {trip.exclusions ? (
                      <p style={{ fontSize: '13px', color: 'var(--tm-slate-500)', marginTop: '4px' }}>
                        <strong>Exclusions:</strong> {trip.exclusions}
                      </p>
                    ) : null}
                  </div>

                  {/* Financial Breakdown */}
                  {breakdown ? (
                    <div className={styles.breakdownCard} style={{ marginTop: 0 }}>
                      <h4 className={styles.breakdownTitle}>Itemized Escrow / Cost Breakdown</h4>
                      <div className={styles.breakdownRow}>
                        <span>Base Agreed Price</span>
                        <span>${breakdown.basePrice}</span>
                      </div>
                      <div className={styles.breakdownRow}>
                        <span>Travelers</span>
                        <span>{breakdown.numberOfTravelers}</span>
                      </div>
                      <div className={styles.breakdownRow}>
                        <span>Per Person Allocation</span>
                        <span>${breakdown.pricePerPerson}</span>
                      </div>
                      <div className={`${styles.breakdownRow} ${styles.breakdownRowTotal}`}>
                        <span>Total Held / Settled Amount</span>
                        <span>${breakdown.totalAmount} {breakdown.currency}</span>
                      </div>
                    </div>
                  ) : null}

                  {/* Itinerary Tracking and Activity Status Updates */}
                  {tracking && tracking.length > 0 ? (
                    <div className={styles.trackingSection}>
                      <h4 style={{ margin: '0 0 12px 0', fontSize: '16px', fontWeight: 700, color: 'var(--tm-slate-900)' }}>
                        Tour Activity Tracking
                      </h4>
                      <p style={{ fontSize: '13px', color: 'var(--tm-slate-500)', margin: '0 0 16px 0' }}>
                        Update the live progress of planned activities during the tour.
                      </p>

                      {tracking.map((day) => (
                        <div className={styles.dayAccordion} key={day.id || day.dayNumber}>
                          <div className={styles.dayHeader}>
                            <span>Day {day.dayNumber}: {day.title}</span>
                            <span style={{ fontSize: '12px', color: 'var(--tm-slate-500)' }}>
                              {(day.activitiesList || []).length} activity(ies)
                            </span>
                          </div>

                          <div className={styles.activityList}>
                            {(!day.activitiesList || day.activitiesList.length === 0) ? (
                              <p style={{ margin: 0, fontSize: '13px', color: 'var(--tm-slate-500)' }}>
                                {day.activities || 'No itemized activities for this day.'}
                              </p>
                            ) : (
                              day.activitiesList.map((act) => (
                                <div className={styles.activityItem} key={act.id}>
                                  <div className={styles.activityInfo}>
                                    <h4>{act.title}</h4>
                                    {act.description ? <p>{act.description}</p> : null}
                                  </div>

                                  <select
                                    aria-label={`Status for ${act.title}`}
                                    className={styles.activityStatusSelect}
                                    disabled={updatingActivityId === act.id}
                                    onChange={(e) =>
                                      handleStatusChange(trip.id, act.id, e.target.value)
                                    }
                                    value={act.status || 'UPCOMING'}
                                  >
                                    <option value="UPCOMING">Upcoming</option>
                                    <option value="IN_PROGRESS">In Progress</option>
                                    <option value="COMPLETED">Completed</option>
                                  </select>
                                </div>
                              ))
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : null}
                </article>
              )
            })}
          </div>
        )}

        {/* Raise Dispute Modal (Phase 7) */}
        {disputeTarget && (
          <RaiseDisputeModal
            isOpen={Boolean(disputeTarget)}
            onClose={() => setDisputeTarget(null)}
            onSuccess={() => {
              setFeedback({
                tone: 'success',
                message: 'Your dispute has been submitted and escalated to platform administration.',
              })
            }}
            token={token}
            tripId={disputeTarget.tripRequestId}
            tripTitle={disputeTarget.tripRequestDestination}
            tripType="TripRequest"
          />
        )}
      </div>
    </DashboardLayout>
  )
}

export default GuideTripsPage

