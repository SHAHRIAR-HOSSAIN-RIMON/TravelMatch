import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import { useAuth } from '../context/useAuth.js'
import { ApiError } from '../lib/api.js'
import { getOpenTripRequests } from '../lib/guideService.js'
import styles from '../components/guide/Guide.module.css'

const SORT_OPTIONS = [
  { value: 0, label: 'Newest First' },
  { value: 1, label: 'Oldest First' },
  { value: 2, label: 'Budget: High to Low' },
  { value: 3, label: 'Budget: Low to High' },
  { value: 4, label: 'Start Date: Soonest' },
]

export function BrowseTripRequestsPage() {
  const { user, token, logout } = useAuth()

  // Filter state
  const [filters, setFilters] = useState({
    destination: '',
    tripType: '',
    startDateFrom: '',
    startDateTo: '',
    minBudget: '',
    maxBudget: '',
    sort: 0,
    page: 1,
    pageSize: 9,
  })

  // Applied query state for backend fetching
  const [activeQuery, setActiveQuery] = useState({
    destination: '',
    tripType: '',
    startDateFrom: '',
    startDateTo: '',
    minBudget: '',
    maxBudget: '',
    sort: 0,
    page: 1,
    pageSize: 9,
  })

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [result, setResult] = useState({
    data: [],
    totalCount: 0,
    totalOpenCount: 0,
    page: 1,
    pageSize: 9,
    totalPages: 1,
    hasFiltersApplied: false,
    guideVerificationStatus: 'Pending',
    canSubmitProposal: false,
  })

  const requestIdRef = useRef(0)

  const fetchRequests = useCallback(async () => {
    if (!token) return

    const requestId = ++requestIdRef.current
    setLoading(true)
    setError(null)

    try {
      const response = await getOpenTripRequests(token, activeQuery)
      if (requestId !== requestIdRef.current) return
      setResult(response)
    } catch (err) {
      if (requestId !== requestIdRef.current) return
      if (err instanceof ApiError && err.status === 401) {
        logout()
        return
      }
      setError(err instanceof Error ? err.message : 'Failed to load trip requests.')
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [activeQuery, logout, token])

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchRequests()
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchRequests])

  function handleFilterChange(field, value) {
    setFilters((prev) => ({ ...prev, [field]: value }))
  }

  function handleApplyFilters(event) {
    event.preventDefault()
    setActiveQuery({
      ...filters,
      page: 1, // reset to first page on new search
    })
  }

  function handleResetFilters() {
    const cleared = {
      destination: '',
      tripType: '',
      startDateFrom: '',
      startDateTo: '',
      minBudget: '',
      maxBudget: '',
      sort: 0,
      page: 1,
      pageSize: 9,
    }
    setFilters(cleared)
    setActiveQuery(cleared)
  }

  function handlePageChange(newPage) {
    if (newPage < 1 || newPage > result.totalPages) return
    setActiveQuery((prev) => ({ ...prev, page: newPage }))
    setFilters((prev) => ({ ...prev, page: newPage }))
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          backTo="/guide/dashboard"
          backLabel="Guide Dashboard"
          eyebrow="Guide Module"
          title="Browse Available Trip Requests"
          description="Explore custom trip requests posted by travelers looking for qualified local guides. Filter by destination, trip dates, and budget."
        />

        {/* Verification Status Banner */}
        {!result.canSubmitProposal && !loading ? (
          <div className={`${styles.noticeBanner} ${styles.noticeWarning}`} role="status">
            <span className={styles.noticeIcon} aria-hidden="true">⚠️</span>
            <div>
              <strong>Guide Verification Pending:</strong> Your guide account is under verification. You can browse requests, but bidding is locked until your profile is verified. Check your <Link to="/profile" style={{ textDecoration: 'underline', fontWeight: 600 }}>Profile</Link> to ensure all details are filled.
            </div>
          </div>
        ) : null}

        {/* Search & Filter Toolbar */}
        <form className={styles.toolbar} onSubmit={handleApplyFilters}>
          <div className={styles.toolbarRow}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-destination">Destination</label>
              <input
                className={styles.fieldInput}
                id="filter-destination"
                onChange={(e) => handleFilterChange('destination', e.target.value)}
                placeholder="Search by city, region, or landmark..."
                type="text"
                value={filters.destination}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-type">Trip Type</label>
              <input
                className={styles.fieldInput}
                id="filter-type"
                onChange={(e) => handleFilterChange('tripType', e.target.value)}
                placeholder="e.g. Hiking, Cultural, Coastal"
                type="text"
                value={filters.tripType}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-sort">Sort By</label>
              <select
                className={styles.fieldSelect}
                id="filter-sort"
                onChange={(e) => handleFilterChange('sort', Number(e.target.value))}
                value={filters.sort}
              >
                {SORT_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>

            <div style={{ display: 'flex', gap: '8px', alignSelf: 'flex-end' }}>
              <button className={styles.primaryButton} style={{ minHeight: '40px', flex: 1 }} type="submit">
                Search
              </button>
              {result.hasFiltersApplied ? (
                <button
                  className={styles.secondaryButton}
                  onClick={handleResetFilters}
                  style={{ minHeight: '40px' }}
                  type="button"
                >
                  Reset
                </button>
              ) : null}
            </div>
          </div>

          {/* Secondary filter row for dates and budget */}
          <div className={styles.toolbarRow} style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-date-from">Earliest Start Date</label>
              <input
                className={styles.fieldInput}
                id="filter-date-from"
                onChange={(e) => handleFilterChange('startDateFrom', e.target.value)}
                type="date"
                value={filters.startDateFrom}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-date-to">Latest Start Date</label>
              <input
                className={styles.fieldInput}
                id="filter-date-to"
                onChange={(e) => handleFilterChange('startDateTo', e.target.value)}
                type="date"
                value={filters.startDateTo}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-min-budget">Min Budget ($)</label>
              <input
                className={styles.fieldInput}
                id="filter-min-budget"
                min="0"
                onChange={(e) => handleFilterChange('minBudget', e.target.value)}
                placeholder="Min USD"
                type="number"
                value={filters.minBudget}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="filter-max-budget">Max Budget ($)</label>
              <input
                className={styles.fieldInput}
                id="filter-max-budget"
                min="0"
                onChange={(e) => handleFilterChange('maxBudget', e.target.value)}
                placeholder="Max USD"
                type="number"
                value={filters.maxBudget}
              />
            </div>
          </div>
        </form>

        {/* Error Notice */}
        {error ? (
          <div className={`${styles.noticeBanner} ${styles.noticeError}`} role="alert">
            <span className={styles.noticeIcon} aria-hidden="true">!</span>
            <div style={{ flex: 1 }}>{error}</div>
            <button className={styles.secondaryButton} onClick={fetchRequests} type="button">
              Retry
            </button>
          </div>
        ) : null}

        {/* Requests List */}
        {loading ? (
          <div className={styles.cardGrid}>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </div>
        ) : result.data.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>
              {result.hasFiltersApplied ? 'No trip requests match your filters' : 'No open trip requests found'}
            </h3>
            <p className={styles.emptyText}>
              {result.hasFiltersApplied
                ? 'Try broadening your search criteria or resetting the filters.'
                : 'Check back later for new trip requests posted by travelers.'}
            </p>
            {result.hasFiltersApplied ? (
              <button className={styles.secondaryButton} onClick={handleResetFilters} type="button">
                Clear Filters
              </button>
            ) : null}
          </div>
        ) : (
          <>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <span style={{ fontSize: '14px', color: 'var(--tm-slate-500)', fontWeight: 500 }}>
                Showing {result.data.length} of {result.totalCount} open request(s)
              </span>
            </div>

            <div className={styles.cardGrid}>
              {result.data.map((request) => (
                <article className={styles.requestCard} key={request.id}>
                  <div>
                    <div className={styles.cardTop}>
                      <div>
                        <h3 className={styles.cardDestination}>{request.destination}</h3>
                        <span className={styles.tripTypeTag}>{request.tripType || 'Tour'}</span>
                      </div>
                      <span className={styles.statusPill} style={{ background: '#f0fdf4', color: '#166534' }}>
                        Open
                      </span>
                    </div>

                    <div className={styles.cardMetaGrid} style={{ marginTop: '16px' }}>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Trip Dates</span>
                        <span className={styles.metaValue}>{request.startDate} to {request.endDate}</span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Group Size</span>
                        <span className={styles.metaValue}>{request.numberOfTravelers} traveler(s)</span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Budget Range</span>
                        <span className={styles.metaValue}>
                          {request.budgetMin && request.budgetMax
                            ? `$${request.budgetMin} – $${request.budgetMax}`
                            : `$${request.budget}`}
                        </span>
                      </div>
                      <div className={styles.metaItem}>
                        <span className={styles.metaLabel}>Created</span>
                        <span className={styles.metaValue}>
                          {new Date(request.createdAt).toLocaleDateString()}
                        </span>
                      </div>
                    </div>

                    <p className={styles.cardSnippet} style={{ marginTop: '14px' }}>
                      {request.description || request.travelPreferences || 'No additional traveler notes provided.'}
                    </p>
                  </div>

                  <div className={styles.cardFooter}>
                    <Link
                      className={styles.primaryButton}
                      style={{ width: '100%' }}
                      to={`/guide/trip-requests/${request.id}`}
                    >
                      {result.canSubmitProposal ? 'View Details & Submit Bid →' : 'View Trip Details →'}
                    </Link>
                  </div>
                </article>
              ))}
            </div>

            {/* Pagination */}
            {result.totalPages > 1 ? (
              <nav aria-label="Trip requests pagination" className={styles.pagination}>
                <button
                  className={styles.secondaryButton}
                  disabled={result.page <= 1}
                  onClick={() => handlePageChange(result.page - 1)}
                  type="button"
                >
                  ← Previous
                </button>
                <span className={styles.pageInfo}>
                  Page {result.page} of {result.totalPages}
                </span>
                <button
                  className={styles.secondaryButton}
                  disabled={result.page >= result.totalPages}
                  onClick={() => handlePageChange(result.page + 1)}
                  type="button"
                >
                  Next →
                </button>
              </nav>
            ) : null}
          </>
        )}
      </div>
    </DashboardLayout>
  )
}

export default BrowseTripRequestsPage

