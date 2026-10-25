import styles from './Tourist.module.css'

const toneClassNames = {
  progress: styles.pillProgress,
  attention: styles.pillAttention,
  closed: styles.pillClosed,
}

function StatusPill({ label, tone = 'closed' }) {
  return (
    <span className={`${styles.pill} ${toneClassNames[tone] ?? styles.pillClosed}`}>
      <span className={styles.pillDot} aria-hidden="true" />
      {label}
    </span>
  )
}

export default StatusPill
