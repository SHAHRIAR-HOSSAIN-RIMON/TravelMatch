import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import { useAuth } from '../../context/useAuth.js'
import { getDashboardSummary } from '../../lib/adminService.js'
import { describeApiError } from '../../lib/api.js'
import styles from '../../components/admin/Admin.module.css'

export function AdminDashboardPage() {
  const { user, token, logout } = useAuth()
  const [summary, setSummary] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const requestIdRef = useRef(0)

  const fetchSummary = useCallback(async () => {
    if (!token) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const data = await getDashboardSummary(token)
      if (requestId !== requestIdRef.current) return
      setSummary(data)
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      setError(describeApiError(err, 'Failed to load platform statistics.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [token])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchSummary()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchSummary])

  const pendingVerifications = summary?.totalPendingGuideVerifications ?? 0
  const openDisputes = summary?.totalOpenDisputes ?? 0

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.header}>
          <h1 className={styles.title}>Admin Control Center</h1>
          <p className={styles.subtitle}>
            Platform-wide operations, moderation, disputes, and verification queue.
          </p>
        </div>

        {error && (
          <div style={{ marginBottom: 24 }}>
            <FeedbackBanner tone="error">{error}</FeedbackBanner>
          </div>
        )}

        {/* Priority Action Alerts */}
        <div className={styles.alertBar}>
          {pendingVerifications > 0 && (
            <div className={`${styles.alertCard} ${styles.alertCardWarning}`}>
              <div>
                <strong>{pendingVerifications} Guide Verification Request{pendingVerifications === 1 ? '' : 's'} Pending:</strong>{' '}
                Guides cannot submit proposals or accept bookings until their credentials are reviewed.
              </div>
              <Link className={styles.alertCardAction} to="/admin/verifications">
                Review Guides →
              </Link>
            </div>
          )}

          {openDisputes > 0 && (
            <div className={`${styles.alertCard} ${styles.alertCardDanger}`}>
              <div>
                <strong>{openDisputes} Open Trip Dispute{openDisputes === 1 ? '' : 's'}:</strong>{' '}
                Escrow funds are frozen on disputed trips pending administrative review and settlement.
              </div>
              <Link className={styles.alertCardAction} to="/admin/disputes">
                Review Disputes →
              </Link>
            </div>
          )}
        </div>

        {/* Aggregate KPI Grid */}
        <h2 className={styles.sectionHeading}>Platform Overview</h2>
        {loading ? (
          <div className={styles.kpiGrid}>
            {Array.from({ length: 6 }).map((_, i) => (
              <div className={styles.kpiCard} key={i}>
                <div className={styles.kpiLabel}>Loading metric…</div>
                <div className={styles.kpiValue} style={{ color: 'var(--tm-slate-300)' }}>—</div>
              </div>
            ))}
          </div>
        ) : summary ? (
          <div className={styles.kpiGrid}>
            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Total Users</div>
              <div className={`${styles.kpiValue} ${styles.kpiValueHighlight}`}>
                {summary.totalUsers}
              </div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Tourists</div>
              <div className={styles.kpiValue}>{summary.totalTourists}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Guides</div>
              <div className={styles.kpiValue}>{summary.totalGuides}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Organizers</div>
              <div className={styles.kpiValue}>{summary.totalOrganizers}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Pending Verifications</div>
              <div className={`${styles.kpiValue} ${pendingVerifications > 0 ? styles.kpiValueAlert : ''}`}>
                {pendingVerifications}
              </div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Open Disputes</div>
              <div className={`${styles.kpiValue} ${openDisputes > 0 ? styles.kpiValueDanger : ''}`}>
                {openDisputes}
              </div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Custom Trip Requests</div>
              <div className={styles.kpiValue}>{summary.totalTripRequests}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Submitted Proposals</div>
              <div className={styles.kpiValue}>{summary.totalProposals}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Organized Trips</div>
              <div className={styles.kpiValue}>{summary.totalOrganizedTrips}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Registrations</div>
              <div className={styles.kpiValue}>{summary.totalRegistrations}</div>
            </div>

            <div className={styles.kpiCard}>
              <div className={styles.kpiLabel}>Escrow Payments</div>
              <div className={styles.kpiValue}>{summary.totalPayments}</div>
            </div>
          </div>
        ) : null}

        {/* Administrative Sections Quick Access */}
        <h2 className={styles.sectionHeading}>Management Modules</h2>
        <div className={styles.navCardsGrid}>
          <Link className={styles.navCard} to="/admin/users">
            <div>
              <div className={styles.navCardTitle}>
                User Management <span>👥</span>
              </div>
              <p className={styles.navCardDesc}>
                Browse all registered users, filter by role (Tourist, Guide, Organizer, Admin), and
                manage account status (active/deactivated).
              </p>
            </div>
            <span className={styles.navCardLink}>Manage users →</span>
          </Link>

          <Link className={styles.navCard} to="/admin/verifications">
            <div>
              <div className={styles.navCardTitle}>
                Guide Verification <span>🛡️</span>
              </div>
              <p className={styles.navCardDesc}>
                Review pending guide verification requests, inspect bios, service areas, and experience,
                and approve or reject guide applications.
              </p>
            </div>
            <span className={styles.navCardLink}>Review verification queue →</span>
          </Link>

          <Link className={styles.navCard} to="/admin/disputes">
            <div>
              <div className={styles.navCardTitle}>
                Dispute Resolution <span>⚖️</span>
              </div>
              <p className={styles.navCardDesc}>
                Manage raised disputes between tourists, guides, and organizers. Review claims, inspect
                escrow details, and settle disputes.
              </p>
            </div>
            <span className={styles.navCardLink}>Manage disputes →</span>
          </Link>
        </div>
      </div>
    </DashboardLayout>
  )
}

export default AdminDashboardPage

