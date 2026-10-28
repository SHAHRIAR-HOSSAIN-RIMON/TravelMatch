import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import RequestDetail from '../components/dashboard/RequestDetail.jsx'
import RequestList from '../components/dashboard/RequestList.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import SummaryBand from '../components/dashboard/SummaryBand.jsx'
import WelcomePanel from '../components/dashboard/WelcomePanel.jsx'
import {
  PROPOSAL_CANDIDATE_LIMIT,
  buildSummaryItems,
  buildWelcomeSentence,
  isClosedStatus,
  mergeProposalResults,
  sortNewestFirst,
  summarizeEscrow,
  summarizeProposals,
  summarizeRequests,
} from '../components/dashboard/dashboardModel.js'
import { useAuth } from '../context/useAuth.js'
import { ApiError } from '../lib/api.js'
import {
  getMyPayments,
  getMyTripRequests,
  getTripRequestProposals,
} from '../lib/touristService.js'
import styles from '../components/dashboard/Dashboard.module.css'

const LOADING = 'loading'
const READY = 'ready'
const ERROR = 'error'

async function readRequests(token) {
  try {
    return { status: READY, data: sortNewestFirst(await getMyTripRequests(token)), error: null }
  } catch (error) {
    return { status: ERROR, data: [], error }
  }
}

async function readPayments(token) {
  try {
    return { status: READY, data: await getMyPayments(token), error: null }
  } catch (error) {
    return { status: ERROR, data: [], error }
  }
}

function requestProposals(token, requestIds) {
  return Promise.allSettled(
    requestIds.map((requestId) => getTripRequestProposals(token, requestId)),
  )
}

function hasExpiredResult(results) {
  return results.some(
    (result) =>
      result.status === 'rejected' &&
      result.reason instanceof ApiError &&
      result.reason.status === 401,
  )
}

function DetailSkeleton() {
  return (
    <div className={styles.detailSkeleton} aria-hidden="true">
      <span className={`${styles.skeletonBlock} ${styles.skeletonHeading}`} />
      <span className={`${styles.skeletonBlock} ${styles.skeletonLineShort}`} />

      <div className={styles.detailSkeletonFacts}>
        <span className={styles.skeletonBlock} />
        <span className={styles.skeletonBlock} />
        <span className={styles.skeletonBlock} />
        <span className={styles.skeletonBlock} />
      </div>
    </div>
  )
}

function TouristDashboardPage() {
  const { user, token, logout } = useAuth()

  const [requestsState, setRequestsState] = useState({ status: LOADING, data: [], error: null })
  const [paymentsState, setPaymentsState] = useState({ status: LOADING, data: [], error: null })
  const [proposalsState, setProposalsState] = useState({ byRequestId: {}, statusById: {} })
  const [selectedRequestId, setSelectedRequestId] = useState(null)

  const detailRef = useRef(null)
  const listHeadingRef = useRef(null)

  const isExpiredSession = useCallback(
    (error) => error instanceof ApiError && error.status === 401,
    [],
  )

  useEffect(() => {
    if (!token) return undefined

    let cancelled = false

    async function load() {
      const next = await readRequests(token)
      if (cancelled) return

      if (next.status === ERROR && isExpiredSession(next.error)) {
        logout()
        return
      }

      setRequestsState(next)
    }

    load()

    return () => {
      cancelled = true
    }
  }, [isExpiredSession, logout, token])

  useEffect(() => {
    if (!token) return undefined

    let cancelled = false

    async function load() {
      const next = await readPayments(token)
      if (cancelled) return

      if (next.status === ERROR && isExpiredSession(next.error)) {
        logout()
        return
      }

      setPaymentsState(next)
    }

    load()

    return () => {
      cancelled = true
    }
  }, [isExpiredSession, logout, token])

  const retryRequests = useCallback(async () => {
    setRequestsState({ status: LOADING, data: [], error: null })

    const next = await readRequests(token)
    if (next.status === ERROR && isExpiredSession(next.error)) {
      logout()
      return
    }

    setRequestsState(next)
  }, [isExpiredSession, logout, token])

  const retryPayments = useCallback(async () => {
    setPaymentsState({ status: LOADING, data: [], error: null })

    const next = await readPayments(token)
    if (next.status === ERROR && isExpiredSession(next.error)) {
      logout()
      return
    }

    setPaymentsState(next)
  }, [isExpiredSession, logout, token])

  const loadProposals = useCallback(
    async (requestIds) => {
      if (!token || requestIds.length === 0) return

      const results = await requestProposals(token, requestIds)
      if (hasExpiredResult(results)) {
        logout()
        return
      }

      setProposalsState((current) => mergeProposalResults(current, requestIds, results))
    },
    [logout, token],
  )

  const candidateIds = useMemo(
    () =>
      requestsState.data
        .filter((request) => !isClosedStatus(request.status))
        .slice(0, PROPOSAL_CANDIDATE_LIMIT)
        .map((request) => request.id),
    [requestsState.data],
  )

  const selectedRequest = useMemo(() => {
    if (requestsState.data.length === 0) return null

    return (
      requestsState.data.find((request) => request.id === selectedRequestId) ??
      requestsState.data[0]
    )
  }, [requestsState.data, selectedRequestId])

  const selectedId = selectedRequest?.id ?? null
  const proposalsStatusById = proposalsState.statusById

  useEffect(() => {
    if (!token || requestsState.status !== READY) return undefined

    const missingIds = candidateIds.filter((requestId) => !proposalsStatusById[requestId])
    if (missingIds.length === 0) return undefined

    let cancelled = false

    async function load() {
      const results = await requestProposals(token, missingIds)
      if (cancelled) return

      if (hasExpiredResult(results)) {
        logout()
        return
      }

      setProposalsState((current) => mergeProposalResults(current, missingIds, results))
    }

    load()

    return () => {
      cancelled = true
    }
  }, [candidateIds, logout, proposalsStatusById, requestsState.status, token])

  useEffect(() => {
    if (!token || selectedId === null) return undefined
    if (candidateIds.includes(selectedId)) return undefined
    if (proposalsStatusById[selectedId]) return undefined

    let cancelled = false
    const requestIds = [selectedId]

    async function load() {
      const results = await requestProposals(token, requestIds)
      if (cancelled) return

      if (hasExpiredResult(results)) {
        logout()
        return
      }

      setProposalsState((current) => mergeProposalResults(current, requestIds, results))
    }

    load()

    return () => {
      cancelled = true
    }
  }, [candidateIds, logout, proposalsStatusById, selectedId, token])

  const requestsSummary = useMemo(() => summarizeRequests(requestsState.data), [requestsState.data])
  const proposalsSummary = useMemo(
    () => summarizeProposals(proposalsState, candidateIds),
    [candidateIds, proposalsState],
  )
  const escrowSummary = useMemo(() => summarizeEscrow(paymentsState.data), [paymentsState.data])

  const welcomeSentence = buildWelcomeSentence({
    status: requestsState.status,
    ...requestsSummary,
  })

  const summaryItems = useMemo(
    () =>
      buildSummaryItems({
        requestsState,
        paymentsState,
        requestsSummary,
        proposalsSummary,
        escrowSummary,
      }),
    [escrowSummary, paymentsState, proposalsSummary, requestsState, requestsSummary],
  )

  const retryProposals = useCallback(
    (requestIds) => {
      if (requestIds.length === 0) return

      setProposalsState((current) => {
        const statusById = { ...current.statusById }
        requestIds.forEach((requestId) => {
          statusById[requestId] = LOADING
        })

        return { ...current, statusById }
      })

      loadProposals(requestIds)
    },
    [loadProposals],
  )

  const handleRetryResource = useCallback(
    (resource) => {
      if (resource === 'requests') {
        retryRequests()
        return
      }

      if (resource === 'payments') {
        retryPayments()
        return
      }

      if (resource === 'proposals') {
        retryProposals(candidateIds)
      }
    },
    [candidateIds, retryPayments, retryProposals, retryRequests],
  )

  function handleSelectRequest(tripRequestId) {
    setSelectedRequestId(tripRequestId)

    if (typeof window === 'undefined') return
    if (!window.matchMedia('(max-width: 960px)').matches) return

    window.requestAnimationFrame(() => {
      detailRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    })
  }

  function handleBackToRequests() {
    listHeadingRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    listHeadingRef.current?.focus({ preventScroll: true })
  }

  const selectedProposals = selectedRequest
    ? {
        status: proposalsState.statusById[selectedRequest.id] ?? LOADING,
        data: proposalsState.byRequestId[selectedRequest.id] ?? [],
      }
    : null

  const announcement = selectedRequest
    ? `Showing details for ${selectedRequest.destination?.trim() || `trip request ${selectedRequest.id}`}.`
    : ''

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <WelcomePanel fullName={user?.fullName ?? 'traveller'} sentence={welcomeSentence} />

        <SummaryBand items={summaryItems} onRetryResource={handleRetryResource} />

        <section className={styles.workspace} aria-label="Your trip requests">
          {requestsState.status === ERROR ? (
            <StateNotice
              actionLabel="Retry trip requests"
              className={styles.workspaceNotice}
              detail="The dashboard could not reach the trip request service. Nothing on this page was changed."
              onAction={retryRequests}
              title="Couldn't load trip requests"
              tone="error"
            />
          ) : null}

          {requestsState.status === READY && requestsState.data.length === 0 ? (
            <StateNotice
              className={styles.workspaceNotice}
              detail="Your trip requests will appear here once you create one. Tell us where you want to go and verified guides will send proposals."
              title="No trip requests yet"
            >
              <Link className={styles.noticeLink} to="/trip-requests/create">
                Create your first trip request
              </Link>
            </StateNotice>
          ) : null}

          {requestsState.status === LOADING || requestsState.data.length > 0 ? (
            <>
              <RequestList
                headingRef={listHeadingRef}
                loading={requestsState.status === LOADING}
                onSelect={handleSelectRequest}
                requests={requestsState.data}
                selectedRequestId={selectedRequest?.id ?? null}
              />

              <div className={styles.detailWrap} ref={detailRef}>
                {selectedRequest && selectedProposals ? (
                  <RequestDetail
                    onBackToRequests={handleBackToRequests}
                    onRetryProposals={() => retryProposals([selectedRequest.id])}
                    proposals={selectedProposals.data}
                    proposalsStatus={selectedProposals.status}
                    request={selectedRequest}
                  />
                ) : (
                  <DetailSkeleton />
                )}
              </div>
            </>
          ) : null}
        </section>

        <p aria-live="polite" className={styles.srOnly} role="status">
          {announcement}
        </p>
      </div>
    </DashboardLayout>
  )
}

export default TouristDashboardPage
