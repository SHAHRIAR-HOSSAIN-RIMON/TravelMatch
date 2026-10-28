import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import StatusPill from '../components/tourist/StatusPill.jsx'
import {
  formatBudget,
  formatDateRange,
  formatMatchingScore,
  formatRating,
  sortNewestFirst,
} from '../components/dashboard/dashboardModel.js'
import { OPEN_TRIP_STATUSES, getProposalStatusMeta } from '../components/tourist/touristModel.js'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from '../lib/api.js'
import { getMyTripRequests, getTripRequestProposals } from '../lib/touristService.js'
import { ERROR, LOADING, READY, useResource } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function proposalMeta(proposal) {
  const guide = proposal.guide ?? {}

  return [
    formatRating(guide.averageRating),
    guide.experienceYears > 0 ? `${guide.experienceYears} years guiding` : null,
    formatMatchingScore(proposal.matchingScore),
    guide.serviceArea ? `Serves ${guide.serviceArea}` : null,
  ]
    .filter(Boolean)
    .join(', ')
}

function ProposalsPage() {
  const { user, token, logout } = useAuth()

  // The API only exposes proposals per trip request, and only for requests that can
  // still receive them, so the open requests are collected first.
  const loader = useCallback(async () => {
    const requests = sortNewestFirst(await getMyTripRequests(token))
    const openRequests = requests.filter((request) => OPEN_TRIP_STATUSES.has(request.status))

    const results = await Promise.allSettled(
      openRequests.map((request) => getTripRequestProposals(token, request.id)),
    )

    const expired = results.find(
      (result) =>
        result.status === 'rejected' &&
        result.reason instanceof ApiError &&
        result.reason.status === 401,
    )

    if (expired) throw expired.reason

    return openRequests
      .map((request, index) => {
        const result = results[index]

        return {
          request,
          proposals: result.status === 'fulfilled' ? result.value : [],
          failed: result.status === 'rejected',
        }
      })
      .filter((group) => group.proposals.length > 0 || group.failed)
  }, [token])

  const { status, data, error, reload } = useResource(loader, logout)
  const groups = data ?? []
  const totalProposals = groups.reduce((total, group) => total + group.proposals.length, 0)

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <PageHeading
          description="Proposals guides have submitted against your open trip requests. The API exposes submitted proposals only, so accepted and rejected ones no longer appear."
          eyebrow="Proposals"
          title="Proposals received"
        >
          <Link className={styles.secondaryButton} to="/trips">
            My trips
          </Link>
        </PageHeading>

        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry proposals"
            detail={describeApiError(error, 'Proposals could not be loaded.')}
            onAction={reload}
            title="Couldn't load proposals"
            tone="error"
          />
        ) : null}

        {status === LOADING ? (
          <div className={styles.grid} aria-hidden="true">
            {[0, 1].map((card) => (
              <div className={styles.skeletonCard} key={card} />
            ))}
          </div>
        ) : null}

        {status === READY && groups.length === 0 ? (
          <StateNotice
            detail="No proposals are waiting on you. Proposals appear once guides respond to a request that is still open, and disappear from this list once you act on them."
            title="Nothing waiting on you"
          >
            <Link className={styles.secondaryButton} to="/trips">
              Review your trip requests
            </Link>
          </StateNotice>
        ) : null}

        {status === READY && groups.length > 0 ? (
          <>
            <p className={styles.sectionLead}>
              {totalProposals} proposal{totalProposals === 1 ? '' : 's'} across {groups.length}{' '}
              request{groups.length === 1 ? '' : 's'}.
            </p>

            <div className={styles.grid}>
              {groups.map((group) => (
                <section className={styles.card} key={group.request.id}>
                  <div className={styles.cardHead}>
                    <div>
                      <h2 className={styles.cardTitle}>
                        {group.request.destination?.trim() || `Trip request #${group.request.id}`}
                      </h2>
                      <p className={styles.cardSubtitle}>
                        {formatDateRange(group.request.startDate, group.request.endDate)}
                        {`, request #${group.request.id}`}
                      </p>
                    </div>
                    <Link className={styles.quietButton} to={`/trips/${group.request.id}`}>
                      Open trip
                    </Link>
                  </div>

                  {group.failed ? (
                    <p className={styles.prose}>
                      Proposals for this request could not be loaded.
                    </p>
                  ) : null}

                  {group.proposals.length > 0 ? (
                    <ul className={styles.list}>
                      {group.proposals.map((proposal) => {
                        const guide = proposal.guide ?? {}
                        const meta = proposalMeta(proposal)
                        const statusMeta = getProposalStatusMeta(proposal.status)

                        return (
                          <li className={styles.listRow} key={proposal.id}>
                            <div className={styles.listRowMain}>
                              <p className={styles.listRowTitle}>
                                {guide.name?.trim() || `Guide #${guide.id ?? proposal.id}`}
                                {guide.isVerified ? (
                                  <span className={styles.verifiedTag}>Verified guide</span>
                                ) : null}
                              </p>
                              {meta ? <p className={styles.listRowMeta}>{meta}</p> : null}
                              {proposal.inclusionsSummary?.trim() ? (
                                <p className={styles.listRowMeta}>
                                  Includes: {proposal.inclusionsSummary}
                                </p>
                              ) : null}
                            </div>

                            <div className={styles.listRowAside}>
                              <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
                              <span className={styles.listRowAmountLabel}>Proposed price</span>
                              <span className={styles.listRowAmount}>
                                {formatBudget({ budget: proposal.price })}
                              </span>
                              <Link
                                className={styles.secondaryButton}
                                to={`/trips/${group.request.id}/proposals/${proposal.id}`}
                              >
                                View proposal
                              </Link>
                            </div>
                          </li>
                        )
                      })}
                    </ul>
                  ) : null}
                </section>
              ))}
            </div>
          </>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default ProposalsPage
