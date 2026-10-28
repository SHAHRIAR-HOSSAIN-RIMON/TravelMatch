import {
  formatAmount,
  formatMatchingScore,
  formatRating,
} from './dashboardModel.js'
import StateNotice from './StateNotice.jsx'
import styles from './Dashboard.module.css'

function proposalMeta(proposal) {
  const guide = proposal.guide ?? {}
  const parts = [
    formatRating(guide.averageRating),
    guide.experienceYears > 0 ? `${guide.experienceYears} years guiding` : null,
    formatMatchingScore(proposal.matchingScore),
    guide.serviceArea ? `Serves ${guide.serviceArea}` : null,
  ].filter(Boolean)

  return parts.join(', ')
}

function ProposalsPanel({ proposals, status, onRetry }) {
  const items = proposals ?? []
  const isReady = status === 'ready'
  const isLoading = status === 'loading'

  return (
    <section className={styles.proposals} aria-labelledby="proposals-heading">
      <div className={styles.proposalsHeader}>
        <h3 className={styles.proposalsTitle} id="proposals-heading">
          Proposals received
        </h3>
        {isReady ? (
          <span aria-hidden="true" className={styles.proposalsCount}>
            {items.length}
          </span>
        ) : null}
      </div>

      {isLoading ? (
        <div className={styles.proposalSkeleton} aria-hidden="true">
          <span className={`${styles.skeletonBlock} ${styles.skeletonLineTitle}`} />
          <span className={`${styles.skeletonBlock} ${styles.skeletonLineMeta}`} />
        </div>
      ) : null}

      {status === 'error' ? (
        <StateNotice
          actionLabel="Retry proposals"
          className={styles.proposalsNotice}
          detail="Submitted proposals for this request could not be loaded."
          onAction={onRetry}
          title="Couldn't load proposals"
          tone="error"
        />
      ) : null}

      {isReady && items.length === 0 ? (
        <p className={styles.proposalsEmpty}>
          No proposals yet. Guides who respond to this request will appear here.
        </p>
      ) : null}

      {isReady && items.length > 0 ? (
        <ul className={styles.proposalRows}>
          {items.map((proposal) => {
            const proposalGuide = proposal.guide ?? {}
            const meta = proposalMeta(proposal)
            const availability = proposal.availability?.trim()
            const inclusions = proposal.inclusionsSummary?.trim()

            return (
              <li className={styles.proposalRow} key={proposal.id}>
                <div className={styles.proposalRowMain}>
                  <p className={styles.proposalGuide}>
                    {proposalGuide.name?.trim() || `Guide #${proposalGuide.id ?? proposal.id}`}
                    {proposalGuide.isVerified ? (
                      <span className={styles.proposalVerified}>Verified guide</span>
                    ) : null}
                  </p>

                  {meta ? <p className={styles.proposalMeta}>{meta}</p> : null}

                  {availability ? (
                    <p className={styles.proposalLine}>
                      <span className={styles.proposalLineLabel}>Availability</span> {availability}
                    </p>
                  ) : null}

                  {inclusions ? (
                    <p className={styles.proposalLine}>
                      <span className={styles.proposalLineLabel}>Includes</span> {inclusions}
                    </p>
                  ) : null}
                </div>

                <div className={styles.proposalPrice}>
                  <span className={styles.proposalPriceLabel}>Proposed price</span>
                  <span className={styles.proposalPriceValue}>
                    {formatAmount(proposal.price) ?? 'Not set'}
                  </span>
                </div>
              </li>
            )
          })}
        </ul>
      ) : null}
    </section>
  )
}

export default ProposalsPanel
