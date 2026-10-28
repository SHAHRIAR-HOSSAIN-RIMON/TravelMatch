import { formatCurrency } from '../dashboard/dashboardModel.js'
import { formatTravelers } from './touristModel.js'
import styles from './Tourist.module.css'

const SOURCE_LABELS = {
  0: 'Guide proposal',
  1: 'Organized trip',
  Proposal: 'Guide proposal',
  Registration: 'Organized trip',
}

function money(value, currency) {
  return formatCurrency(value, currency) ?? '—'
}

/**
 * Renders the amounts returned by api/cost-breakdown verbatim. BasePrice is the
 * guide's quoted total for a proposal and the per-seat price for a registration,
 * so it is shown alongside the per-person and total figures the API computes.
 */
function CostBreakdownCard({ breakdown, heading = 'Cost breakdown' }) {
  if (!breakdown) return null

  const currency = breakdown.currency
  const source = SOURCE_LABELS[breakdown.breakdownType]

  return (
    <section className={styles.card} aria-label={heading}>
      <div className={styles.cardHead}>
        <h2 className={styles.cardTitle}>{heading}</h2>
        {source ? <span className={styles.verifiedTag}>{source}</span> : null}
      </div>

      <dl className={styles.breakdownRows}>
        <div className={styles.breakdownRow}>
          <dt className={styles.breakdownLabel}>Base price</dt>
          <dd className={styles.breakdownValue}>{money(breakdown.basePrice, currency)}</dd>
        </div>

        <div className={styles.breakdownRow}>
          <dt className={styles.breakdownLabel}>Price per traveller</dt>
          <dd className={styles.breakdownValue}>{money(breakdown.pricePerPerson, currency)}</dd>
        </div>

        <div className={styles.breakdownRow}>
          <dt className={styles.breakdownLabel}>Travellers</dt>
          <dd className={styles.breakdownValue}>{formatTravelers(breakdown.numberOfTravelers)}</dd>
        </div>

        <div className={`${styles.breakdownRow} ${styles.breakdownTotal}`}>
          <dt className={styles.breakdownLabel}>Total</dt>
          <dd className={styles.breakdownValue}>{money(breakdown.totalAmount, currency)}</dd>
        </div>
      </dl>

      {breakdown.inclusions ? (
        <p className={styles.breakdownNote}>
          <strong>Includes:</strong> {breakdown.inclusions}
        </p>
      ) : null}

      {breakdown.exclusions ? (
        <p className={styles.breakdownNote}>
          <strong>Excludes:</strong> {breakdown.exclusions}
        </p>
      ) : null}

      <p className={styles.breakdownNote}>
        Amounts are returned by the server in {currency ?? 'the recorded currency'}. No platform
        fee or tax lines are applied.
      </p>
    </section>
  )
}

export default CostBreakdownCard
