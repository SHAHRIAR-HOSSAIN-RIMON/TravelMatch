import styles from './Messaging.module.css'

export default function ConnectionPill({ state, readOnly }) {
  const live = state === 'Connected'
  const connecting = state === 'Connecting' || state === 'Reconnecting'
  return <span role="status" className={`${styles.pill} ${live ? styles.pillConnected : connecting ? styles.pillConnecting : styles.pillOffline}`}>
    {readOnly ? 'History' : live ? 'Live' : connecting ? 'Connecting…' : 'Offline'}
  </span>
}
