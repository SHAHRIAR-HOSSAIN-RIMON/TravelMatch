import { useCallback, useRef, useState } from 'react'
import StateNotice from '../dashboard/StateNotice.jsx'
import FeedbackBanner from '../tourist/FeedbackBanner.jsx'
import ConfirmDialog from '../tourist/ConfirmDialog.jsx'
import ApplicationPagination from './ApplicationPagination.jsx'
import { formatBudget } from '../dashboard/dashboardModel.js'
import { ApiError, describeApiError } from '../../lib/api.js'
import { getOrganizerApplications, selectOrganizedTripGuide } from '../../lib/guideApplicationService.js'
import { useResource, READY, ERROR, LOADING } from '../../lib/useResource.js'
import styles from '../tourist/Tourist.module.css'

export default function GuideApplicationsPanel({ trip, token, logout, onSelected }) {
  const [page, setPage] = useState(1)
  const [target, setTarget] = useState(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState(null)
  const selecting = useRef(false)
  const loader = useCallback(() => getOrganizerApplications(token, trip.id, page), [token, trip.id, page])
  const { data, status, error, reload } = useResource(loader, logout)
  const canSelect = ['ApplicationOpen', 'GuideSelection'].includes(trip.status)
  async function select() {
    if (!target || selecting.current) return
    selecting.current = true
    setBusy(true)
    setActionError(null)
    try {
      await selectOrganizedTripGuide(token, trip.id, target.id)
      setTarget(null)
      onSelected()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) logout()
      setActionError(describeApiError(err, 'The guide could not be selected.'))
    } finally {
      selecting.current = false
      setBusy(false)
    }
  }
  return <section className={styles.panel} aria-label="Guide applications">
    <div className={styles.panelHead}><h2 className={styles.sectionTitle}>Guide applications</h2>
      <button className={styles.secondaryButton} type="button" onClick={reload} disabled={busy}>Refresh applications</button>
    </div>
    {canSelect && <p>Review offers and select one guide. Selection opens tourist registration and rejects the other pending applications.</p>}
    {status === LOADING && <p role="status">Loading applications…</p>}
    {status === ERROR && <StateNotice title="Couldn't load applications" detail={describeApiError(error)} tone="error" actionLabel="Retry" onAction={reload} />}
    {status === READY && <>
      {data.data.length === 0 && <p>No guide applications yet. Guides can find this trip under Organized Trips once it is published.</p>}
      <div className={styles.grid}>{data.data.map((application) => <article className={styles.card} key={application.id}>
        <h3 className={styles.cardTitle}>{application.guideName}</h3>
        <p>Proposed fee: {formatBudget({ budget: application.proposedPrice })}</p>
        <p>Status: <strong>{application.status}</strong></p>
        <p className={styles.prose}>{application.message}</p>
        {canSelect && application.status === 'Pending' && <button className={styles.primaryButton} type="button" disabled={busy} onClick={() => { setActionError(null); setTarget(application) }}>Select guide</button>}
      </article>)}</div>
      <ApplicationPagination page={page} totalPages={data.totalPages} onChange={setPage} />
    </>}
    <ConfirmDialog open={Boolean(target)} title={`Select ${target?.guideName || 'this guide'}?`} description="This opens tourist registration and rejects all other pending applications. The selected guide cannot be changed here." confirmLabel="Select guide and open registration" busy={busy} onCancel={() => setTarget(null)} onConfirm={select}>
      <FeedbackBanner tone="error">{actionError}</FeedbackBanner>
    </ConfirmDialog>
  </section>
}
