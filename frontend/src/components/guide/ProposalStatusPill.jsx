import styles from './Guide.module.css'

const statusMap = {
  Draft: { label: 'Draft', className: styles.pillDraft },
  Submitted: { label: 'Submitted', className: styles.pillSubmitted },
  Accepted: { label: 'Accepted', className: styles.pillAccepted },
  Rejected: { label: 'Declined', className: styles.pillRejected },
  Withdrawn: { label: 'Withdrawn', className: styles.pillWithdrawn },
}

export function ProposalStatusPill({ status }) {
  const meta = statusMap[status] ?? {
    label: status || 'Unknown',
    className: styles.pillWithdrawn,
  }

  return (
    <span className={`${styles.statusPill} ${meta.className}`}>
      <span className={styles.statusPillDot} aria-hidden="true" />
      {meta.label}
    </span>
  )
}

export default ProposalStatusPill

