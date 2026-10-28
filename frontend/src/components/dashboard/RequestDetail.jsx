import { Link } from 'react-router-dom'
import ProposalsPanel from './ProposalsPanel.jsx'
import StatusBadge from './StatusBadge.jsx'
import {
  formatBudget,
  formatDateRange,
  formatLongDate,
  formatNights,
} from './dashboardModel.js'
import styles from './Dashboard.module.css'

function fieldOrFallback(value) {
  const text = typeof value === 'string' ? value.trim() : ''
  return text || 'Not provided'
}

function RequestDetail({
  request,
  proposals,
  proposalsStatus,
  onRetryProposals,
  onBackToRequests,
}) {
  const title = request.destination?.trim() || `Trip request #${request.id}`
  const requestedOn = formatLongDate(request.createdAt)
  const nights = formatNights(request.startDate, request.endDate)
  const travelerCount = Number(request.numberOfTravelers)
  const travelers = Number.isFinite(travelerCount) && travelerCount > 0
    ? `${travelerCount} traveller${travelerCount === 1 ? '' : 's'}`
    : 'Not provided'
  const description = request.description?.trim()
  const preferences = request.travelPreferences?.trim()

  return (
    <section className={styles.detail} id="request-detail" aria-labelledby="request-detail-heading">
      <button className={styles.backButton} onClick={onBackToRequests} type="button">
        <span className={styles.backIcon} aria-hidden="true" />
        Back to recent requests
      </button>

      <h2 className={styles.detailHeading} id="request-detail-heading">
        {title}
      </h2>

      <div className={styles.detailStatusRow}>
        <StatusBadge status={request.status} />
        <span className={styles.detailMeta}>
          {requestedOn ? `Request #${request.id}, created ${requestedOn}` : `Request #${request.id}`}
        </span>
      </div>

      <dl className={styles.detailFacts}>
        <div className={styles.fact}>
          <dt className={styles.factLabel}>
            <span className={styles.factIconCalendar} aria-hidden="true" />
            Travel dates
          </dt>
          <dd className={styles.factValue}>
            {formatDateRange(request.startDate, request.endDate)}
            {nights ? <span className={styles.factNote}>{nights}</span> : null}
          </dd>
        </div>

        <div className={styles.fact}>
          <dt className={styles.factLabel}>
            <span className={styles.factIconUsers} aria-hidden="true" />
            Travellers
          </dt>
          <dd className={styles.factValue}>{travelers}</dd>
        </div>

        <div className={styles.fact}>
          <dt className={styles.factLabel}>
            <span className={styles.factIconWallet} aria-hidden="true" />
            Budget
          </dt>
          <dd className={styles.factValue}>
            {formatBudget(request)}
            <span className={styles.factNote}>No currency recorded</span>
          </dd>
        </div>

        <div className={styles.fact}>
          <dt className={styles.factLabel}>Trip type</dt>
          <dd className={styles.factValue}>{fieldOrFallback(request.tripType)}</dd>
        </div>
      </dl>

      {description ? (
        <div className={styles.detailSection}>
          <h3 className={styles.detailSubheading}>Description</h3>
          <p className={styles.detailParagraph}>{description}</p>
        </div>
      ) : null}

      {preferences ? (
        <div className={styles.detailSection}>
          <h3 className={styles.detailSubheading}>Travel preferences</h3>
          <p className={styles.detailParagraph}>{preferences}</p>
        </div>
      ) : null}

      <ProposalsPanel
        onRetry={onRetryProposals}
        proposals={proposals}
        status={proposalsStatus}
      />

      <div className={styles.detailSection}>
        <Link className={styles.retryButton} to={`/trips/${request.id}`}>
          Open full trip details
        </Link>
      </div>

      <p className={styles.readOnlyNote}>
        This summary is read-only. Editing a request, accepting a proposal and advancing the trip
        lifecycle all happen on the trip detail page. Budgets and proposal prices are shown as
        recorded because the API does not return a currency for them.
      </p>
    </section>
  )
}

export default RequestDetail
