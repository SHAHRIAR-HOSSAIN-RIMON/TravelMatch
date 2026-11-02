import { useCallback, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../components/dashboard/StateNotice.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import { formatDateRange } from '../components/dashboard/dashboardModel.js'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from '../lib/api.js'
import { getGuideOrganizedTrip, findMyTripApplication, applyForOrganizedTrip } from '../lib/guideApplicationService.js'
import { useResource, READY, ERROR, LOADING } from '../lib/useResource.js'
import styles from '../components/tourist/Tourist.module.css'

function ApplicationForm({ token, tripId, logout, onSubmitted }) {
  const [price, setPrice] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const submitting = useRef(false)
  async function submit(event) {
    event.preventDefault()
    if (submitting.current) return
    if (!message.trim() || !Number.isFinite(Number(price)) || Number(price) <= 0) {
      setError('Enter a positive price and a message explaining your suitability.')
      return
    }
    submitting.current = true
    setBusy(true)
    setError(null)
    try {
      await applyForOrganizedTrip(token, tripId, { proposedPrice: Number(price), message: message.trim() })
      onSubmitted()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) logout()
      setError(describeApiError(err, 'Your application could not be submitted.'))
    } finally {
      submitting.current = false
      setBusy(false)
    }
  }
  return <form className={styles.form} onSubmit={submit}>
    <h2 className={styles.sectionTitle}>Apply to guide this trip</h2>
    <FeedbackBanner tone="error">{error}</FeedbackBanner>
    <label className={styles.field}>Your proposed guide fee
      <input className={styles.input} type="number" min="0.01" step="0.01" required value={price} onChange={(e) => setPrice(e.target.value)} disabled={busy} />
    </label>
    <label className={styles.field}>Message to the organizer
      <textarea className={styles.textarea} required maxLength={2000} value={message} onChange={(e) => setMessage(e.target.value)} disabled={busy} />
    </label>
    <button className={styles.primaryButton} type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit application'}</button>
  </form>
}

export default function GuideOrganizedTripDetailPage() {
  const { tripId } = useParams()
  const id = Number(tripId)
  const { user, token, logout } = useAuth()
  const [submitted, setSubmitted] = useState(false)
  const loader = useCallback(async () => {
    if (!Number.isSafeInteger(id) || id <= 0) throw new Error('Invalid trip link.')
    const [trip, application] = await Promise.all([getGuideOrganizedTrip(token, id), findMyTripApplication(token, id)])
    return { ...trip, application }
  }, [token, id])
  const { data, status, error, reload } = useResource(loader, logout)
  const trip = status === READY ? data.data : null
  return <DashboardLayout user={user} onLogout={logout}><div className={styles.container}>
    <PageHeading title={trip?.title || 'Organized trip'} backTo="/guide/organized-trips" backLabel="Organized trips" />
    {submitted && <FeedbackBanner tone="success">Application submitted. The organizer will review your offer.</FeedbackBanner>}
    {status === LOADING && <p role="status">Loading trip…</p>}
    {status === ERROR && <StateNotice title="Couldn't load trip" detail={describeApiError(error)} tone="error" actionLabel="Retry" onAction={reload} />}
    {trip && <section className={styles.panel}>
      {data.application?.status === 'Accepted' && <Link className={styles.primaryButton} to={`/organized-trips/${id}/chat`}>Trip group chat</Link>}
      <p>{trip.destination} · {formatDateRange(trip.startDate, trip.endDate)}</p>
      <p>Status: {trip.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</p>
      <p className={styles.prose}>{trip.description}</p>
      {data.application ? <FeedbackBanner tone={data.application.status === 'Accepted' ? 'success' : 'info'}>
        Your application is {data.application.status.toLowerCase()}.{data.application.status === 'Accepted' ? ' You are the selected guide for this trip.' : ''}
      </FeedbackBanner> : trip.status !== 'ApplicationOpen' ? <FeedbackBanner>This trip is no longer accepting guide applications.</FeedbackBanner>
        : !data.canApply ? <FeedbackBanner>Your guide profile must be verified before you can apply.</FeedbackBanner>
          : <ApplicationForm key={id} token={token} tripId={id} logout={logout} onSubmitted={() => { setSubmitted(true); reload() }} />}
      <button className={styles.secondaryButton} type="button" onClick={reload}>Refresh status</button>
    </section>}
  </div></DashboardLayout>
}
