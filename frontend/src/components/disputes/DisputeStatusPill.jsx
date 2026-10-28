import styles from './Disputes.module.css'

export function DisputeStatusPill({ status }) {
  const normalized = status ? String(status).trim() : 'Open'

  let className = styles.pillOpen
  if (normalized === 'Resolved') {
    className = styles.pillResolved
  } else if (normalized === 'Closed') {
    className = styles.pillClosed
  }

  return (
    <span className={`${styles.disputePill} ${className}`}>
      <span className={styles.pillDot} aria-hidden="true" />
      {normalized}
    </span>
  )
}

export default DisputeStatusPill

