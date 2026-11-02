import { useEffect, useRef, useState } from 'react'
import { HUB_PATHS } from '../../lib/signalrConnection.js'
import { createActivity, deleteActivity, getItineraryTracking, updateActivityStatus, ACTIVITY_STATUSES } from '../../lib/trackingService.js'
import { describeApiError } from '../../lib/api.js'
import { useProposalLiveData } from '../../lib/useProposalLiveData.js'
import { reduceTracking, TRACKING_EVENTS } from '../../lib/proposalLiveModel.js'
import ConnectionPill from './ConnectionPill.jsx'
import styles from './Messaging.module.css'

const STATUS_LABELS = { UPCOMING: 'Upcoming', IN_PROGRESS: 'In progress', COMPLETED: 'Completed' }

function AddActivityForm({ dayId, onAdd, busy }) {
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  if (!open) return <button className={styles.secondaryButton} type="button" disabled={busy} onClick={() => setOpen(true)}>+ Add activity</button>
  return <form className={styles.addActivityForm} onSubmit={async (event) => {
    event.preventDefault()
    if (await onAdd({ itineraryDayId: dayId, title: title.trim(), description: description.trim() })) {
      setOpen(false); setTitle(''); setDescription('')
    }
  }}>
    <label>Activity title<input className={styles.addInput} required maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} /></label>
    <label>Description (optional)<input className={styles.addInput} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} /></label>
    <div className={styles.activityActions}>
      <button className={styles.sendButton} type="submit" disabled={busy || !title.trim()}>Add</button>
      <button className={styles.secondaryButton} type="button" disabled={busy} onClick={() => setOpen(false)}>Cancel</button>
    </div>
  </form>
}

export default function ItineraryTrackingPanel({ proposalId, token, isGuide, readOnly = false }) {
  const { data: days, loading, error, connectionState, connectionError, restricted, refresh, apply } = useProposalLiveData({
    proposalId, token, hubPath: HUB_PATHS.itineraryTracking, joinMethod: 'JoinProposalGroup',
    leaveMethod: 'LeaveProposalGroup', load: getItineraryTracking, reduce: reduceTracking, events: TRACKING_EVENTS, readOnly,
  })
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState(null)
  const active = useRef(false)
  const mutationLock = useRef(false)
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  const canEdit = isGuide && !readOnly && !restricted
  const activities = days.flatMap((day) => day.activitiesList)
  const completed = activities.filter((a) => a.status === 'COMPLETED').length

  async function mutate(operation, event, payload) {
    if (!canEdit || mutationLock.current) return false
    mutationLock.current = true
    setBusy(true)
    setActionError(null)
    try {
      const result = await operation()
      // Apply the HTTP result even while disconnected; broadcasts are deduplicated.
      if (active.current) apply(event, payload ?? result)
      return true
    } catch (err) {
      if (active.current) setActionError(describeApiError(err, 'Activity could not be updated.'))
      return false
    } finally {
      mutationLock.current = false
      if (active.current) setBusy(false)
    }
  }

  return <section className={styles.panel} aria-label="Itinerary tracking">
    <div className={styles.panelHeader}>
      <h2 className={styles.panelTitle}>Itinerary Tracking</h2>
      <ConnectionPill state={connectionState} readOnly={readOnly || restricted} />
    </div>
    <p className={styles.panelLead}>{readOnly ? 'Completed trip itinerary.' : isGuide ? 'Update activities as your trip progresses. Your tourist sees changes live.' : 'Follow your guide’s activity updates live.'}</p>
    {connectionError ? <p className={styles.inlineError} role="status">{connectionError}</p> : null}
    {error ? <div className={styles.inlineError} role="alert">{error} <button type="button" onClick={refresh}>Retry itinerary</button></div> : null}
    {actionError ? <p className={styles.inlineError} role="alert">{actionError}</p> : null}
    {loading ? <p>Loading itinerary…</p> : null}
    {!loading && !days.length ? <p className={styles.emptyChat}>No itinerary days have been planned yet.</p> : null}
    {activities.length ? <div className={styles.progress}>
      <label htmlFor={`trip-progress-${proposalId}`}>{completed} of {activities.length} activities completed</label>
      <progress id={`trip-progress-${proposalId}`} max={activities.length} value={completed} />
    </div> : null}
    <div className={styles.dayList}>{days.map((day) => <div className={styles.dayCard} key={day.id}>
      <h3 className={styles.dayTitle}>Day {day.dayNumber}{day.title ? `: ${day.title}` : ''}</h3>
      {day.description ? <p className={styles.panelLead}>{day.description}</p> : null}
      {day.activities ? <p className={styles.panelLead}>{day.activities}</p> : null}
      {!day.activitiesList.length ? <p className={styles.hint}>No itemized activities yet.</p> : null}
      <ol className={styles.activityList}>{[...day.activitiesList].sort((a, b) => a.orderIndex - b.orderIndex).map((activity) => <li className={styles.activityRow} key={activity.id}>
        <div className={styles.activityBody}><strong>{activity.title}</strong>{activity.description ? <p className={styles.panelLead}>{activity.description}</p> : null}</div>
        <div className={styles.activityActions}>{canEdit ? <>
          <select className={styles.statusSelect} aria-label={`Status for ${activity.title}`} disabled={busy} value={activity.status}
            onChange={(e) => mutate(() => updateActivityStatus(token, activity.id, e.target.value), 'ActivityStatusUpdated')}>
            {ACTIVITY_STATUSES.map((status) => <option key={status} value={status}>{STATUS_LABELS[status]}</option>)}
          </select>
          <button className={styles.secondaryButton} type="button" disabled={busy} aria-label={`Delete ${activity.title}`}
            onClick={() => mutate(() => deleteActivity(token, activity.id), 'ActivityDeleted', { activityId: activity.id })}>Delete</button>
        </> : <span className={`${styles.activityStatus} ${activity.status === 'COMPLETED' ? styles.pillConnected : activity.status === 'IN_PROGRESS' ? styles.pillConnecting : styles.pillOffline}`}>{STATUS_LABELS[activity.status] ?? activity.status}</span>}</div>
      </li>)}</ol>
      {canEdit ? <AddActivityForm dayId={day.id} busy={busy} onAdd={(payload) => mutate(() => createActivity(token, payload), 'ActivityCreated')} /> : null}
    </div>)}</div>
  </section>
}
