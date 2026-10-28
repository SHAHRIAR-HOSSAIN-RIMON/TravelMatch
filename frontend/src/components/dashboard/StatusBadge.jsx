import { getStatusMeta } from './dashboardModel.js'
import styles from './Dashboard.module.css'

const toneClassNames = {
  attention: styles.statusAttention,
  progress: styles.statusProgress,
  closed: styles.statusClosed,
}

function StatusBadge({ status }) {
  const meta = getStatusMeta(status)
  const toneClassName = toneClassNames[meta.tone] ?? styles.statusClosed

  return (
    <span className={`${styles.status} ${toneClassName}`}>
      <span className={styles.statusDot} aria-hidden="true" />
      {meta.label}
    </span>
  )
}

export default StatusBadge
