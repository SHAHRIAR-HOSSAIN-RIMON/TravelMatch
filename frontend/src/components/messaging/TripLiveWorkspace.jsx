import { useEffect, useState } from 'react'
import { getTripRequestLifecycle } from '../../lib/touristService.js'
import { describeApiError } from '../../lib/api.js'
import TripChatPanel from './TripChatPanel.jsx'
import ItineraryTrackingPanel from './ItineraryTrackingPanel.jsx'
import styles from './Messaging.module.css'

const ACTIVE_STATUSES = ['Matched', 'PaymentPending', 'Planned', 'InProgress']

export default function TripLiveWorkspace(props) {
  return <Workspace key={`${props.currentUserId}:${props.proposalId}:${props.token}`} {...props} />
}

function Workspace({ proposalId, tripRequestId, token, currentUserId, isGuide = false, tripStatus }) {
  const [lifecycle, setLifecycle] = useState(null)
  const [error, setError] = useState(null)
  useEffect(() => {
    if (isGuide || tripStatus || !tripRequestId) return
    let disposed = false
    let busy = false
    async function refresh() {
      if (busy || document.visibilityState === 'hidden') return
      busy = true
      try {
        const result = await getTripRequestLifecycle(token, tripRequestId)
        if (!disposed) { setLifecycle(result); setError(null) }
      } catch (err) {
        if (!disposed) setError(describeApiError(err))
      } finally { busy = false }
    }
    void refresh()
    const timer = setInterval(refresh, 15_000)
    document.addEventListener('visibilitychange', refresh)
    return () => { disposed = true; clearInterval(timer); document.removeEventListener('visibilitychange', refresh) }
  }, [token, tripRequestId, tripStatus, isGuide])
  const status = tripStatus ?? lifecycle?.status
  if (!isGuide && (!status || (!ACTIVE_STATUSES.includes(status) && status !== 'Completed'))) {
    return error ? <p className={styles.inlineError} role="alert">{error}</p> : null
  }
  const readOnly = status === 'Completed'
  return <div className={styles.workspace}>
    {status ? <div className={styles.tripStatus} role="status"><strong>Trip status: {status.replace(/([a-z])([A-Z])/g, '$1 $2')}</strong><span className={styles.hint}>Status refreshes automatically</span></div> : null}
    {error ? <p className={styles.inlineError} role="alert">{error}</p> : null}
    <div className={styles.panels}>
      <TripChatPanel key={`chat:${readOnly}`} proposalId={Number(proposalId)} token={token} currentUserId={currentUserId} readOnly={readOnly} />
      <ItineraryTrackingPanel key={`tracking:${readOnly}`} proposalId={Number(proposalId)} token={token} isGuide={isGuide} readOnly={readOnly} />
    </div>
  </div>
}
