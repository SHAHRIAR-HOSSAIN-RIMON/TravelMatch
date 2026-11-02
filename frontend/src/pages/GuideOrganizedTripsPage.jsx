import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import ApplicationPagination from '../components/organizer/ApplicationPagination.jsx'
import { formatBudget, formatDateRange } from '../components/dashboard/dashboardModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError } from '../lib/api.js'
import { getGuideOrganizedTrips, getGuideApplications } from '../lib/guideApplicationService.js'
import { useResource, READY, ERROR, LOADING } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function TripList({ mine, token, logout }) {
  const [page, setPage] = useState(1)
  const loader = useCallback(() => (mine ? getGuideApplications : getGuideOrganizedTrips)(token, page), [mine, token, page])
  const { data, status, error, reload } = useResource(loader, logout)
  return <>
    {status === LOADING && <p role="status">Loading trips…</p>}
    {status === ERROR && <StateNotice title="Couldn't load trips" detail={describeApiError(error)} tone="error" actionLabel="Retry" onAction={reload} />}
    {status === READY && <>
      {!mine && !data.canApply && <FeedbackBanner>You can browse trips. Your guide profile must be verified before you can apply.</FeedbackBanner>}
      <button className={styles.secondaryButton} type="button" onClick={reload}>Refresh</button>
      {data.data.length === 0 && <StateNotice title={mine ? 'No applications yet' : 'No trips accepting applications'} detail={mine ? 'Browse open trips to send your first application.' : 'Published trips appear here when organizers are looking for a guide.'} />}
      <div className={styles.grid}>
        {data.data.map((item) => <article className={styles.card} key={item.id}>
          <h2 className={styles.cardTitle}>{mine ? item.tripTitle : item.title}</h2>
          {mine ? <>
            <p>Application: <strong>{item.status}</strong></p>
            <p>Your quote: {formatBudget({ budget: item.proposedPrice })}</p>
            <p className={styles.prose}>{item.message}</p>
            {item.status === 'Accepted' && <FeedbackBanner tone="success">You have been selected to guide this trip. Tourist registration is now open unless the trip has since closed.</FeedbackBanner>}
          </> : <>
            <p>{item.destination} · {formatDateRange(item.startDate, item.endDate)}</p>
            <p className={styles.prose}>{item.description}</p>
          </>}
          <Link className={styles.primaryButton} to={`/guide/organized-trips/${mine ? item.tripId : item.id}`}>View trip{mine ? '' : ' and apply'}</Link>
        </article>)}
      </div>
      <ApplicationPagination page={page} totalPages={data.totalPages} onChange={setPage} />
    </>}
  </>
}

export default function GuideOrganizedTripsPage() {
  const { user, token, logout } = useAuth()
  const [mine, setMine] = useState(false)
  return <DashboardLayout user={user} onLogout={logout}><div className={styles.container}>
    <PageHeading eyebrow="Guide" title="Organized trips" description="Apply to lead an organizer’s group trip and track your applications." />
    <div className={styles.formActions}>
      <button className={styles.secondaryButton} aria-pressed={!mine} onClick={() => setMine(false)} type="button">Open trips</button>
      <button className={styles.secondaryButton} aria-pressed={mine} onClick={() => setMine(true)} type="button">My applications & selected trips</button>
    </div>
    <TripList key={String(mine)} mine={mine} token={token} logout={logout} />
  </div></DashboardLayout>
}
