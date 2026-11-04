import { useEffect, useId, useRef, useState } from 'react'
import { api, describeApiError } from '../../lib/api.js'
import ConfirmDialog from '../tourist/ConfirmDialog.jsx'
import styles from './Guide.module.css'

function unwrap(response) {
  if (response?.success === false) throw new Error(response.message || 'Could not save itinerary.')
  return response?.data
}

export default function ItineraryEditor({ proposalId, token, proposalStatus, onChanged }) {
  const [days, setDays] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [retry, setRetry] = useState(0)
  const [form, setForm] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [message, setMessage] = useState(null)
  const [deleting, setDeleting] = useState(null)
  const lock = useRef(false)
  const prefix = useId()
  const editable = ['Draft', 'Submitted', 'Accepted'].includes(proposalStatus)

  useEffect(() => {
    let active = true
    api.get(`/api/Itinerary/proposals/${proposalId}/days`, { token })
      .then((response) => {
        const data = unwrap(response)
        if (active) { setDays(data ?? []); setLoadError(null) }
      })
      .catch((err) => { if (active) setLoadError(describeApiError(err, 'Could not load itinerary.')) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [proposalId, token, retry])

  function openForm(day) {
    setError(null)
    setMessage(null)
    setForm(day ? { ...day } : {
      dayNumber: Math.max(0, ...days.map((item) => item.dayNumber)) + 1,
      title: '', activities: '', schedule: '',
    })
  }

  async function save(event) {
    event.preventDefault()
    if (lock.current || !form || !editable) return
    const payload = { title: form.title.trim(), activities: form.activities.trim(), schedule: (form.schedule ?? '').trim() }
    const dayNumber = Number(form.dayNumber)
    if (!payload.title || !payload.activities) { setError('Enter a title and planned activities.'); return }
    if (!form.id && (!Number.isInteger(dayNumber) || dayNumber < 1 || days.some((day) => day.dayNumber === dayNumber))) {
      setError('Choose a positive day number that is not already used.'); return
    }
    lock.current = true
    setBusy(true)
    setError(null)
    try {
      const response = form.id
        ? await api.put(`/api/Itinerary/days/${form.id}`, payload, { token })
        : await api.post(`/api/Itinerary/proposals/${proposalId}/days`, { ...payload, dayNumber }, { token })
      const saved = unwrap(response)
      setDays((previous) => [...previous.filter((day) => day.id !== saved.id), saved].sort((a, b) => a.dayNumber - b.dayNumber))
      setForm(null)
      setMessage('Itinerary day saved. After acceptance, add individual activities in Itinerary Tracking below.')
      onChanged?.()
    } catch (err) { setError(describeApiError(err, 'Could not save the day. Your entries have been kept.')) }
    finally { lock.current = false; setBusy(false) }
  }

  async function remove() {
    if (lock.current || !deleting || !editable) return
    lock.current = true
    setBusy(true)
    setError(null)
    try {
      unwrap(await api.delete(`/api/Itinerary/days/${deleting.id}`, { token }))
      setDays((previous) => previous.filter((day) => day.id !== deleting.id))
      setDeleting(null)
      setMessage('Itinerary day deleted.')
      onChanged?.()
    } catch (err) { setError(describeApiError(err, 'Could not delete the day.')); setDeleting(null) }
    finally { lock.current = false; setBusy(false) }
  }

  return <section className={styles.contentBlock} aria-label="Plan itinerary" style={{ marginTop: 28, borderTop: '1px solid var(--tm-border-soft)', paddingTop: 20 }}>
    <h3>Day-by-day itinerary</h3>
    <p>Plan what happens each day. Once the tourist accepts your proposal, add individual activities in Itinerary Tracking and update their progress.</p>
    {loading ? <p>Loading itinerary…</p> : loadError ? <div role="alert">
      <p>{loadError}</p><button type="button" className={styles.secondaryButton} onClick={() => { setLoading(true); setRetry((value) => value + 1) }}>Retry itinerary</button>
    </div> : <>
      {message ? <p role="status">{message}</p> : null}
      {error ? <p className={styles.fieldError} role="alert">{error}</p> : null}
      {!days.length ? <p>No days planned yet. Add Day 1 to get started.</p> : null}
      <div style={{ display: 'grid', gap: 16 }}>
        {days.map((day) => <article key={day.id} style={{ padding: 16, border: '1px solid var(--tm-border-soft)', borderRadius: 12, overflowWrap: 'anywhere' }}>
          <strong>Day {day.dayNumber}: {day.title}</strong>
          <p style={{ whiteSpace: 'pre-wrap' }}>{day.activities}</p>
          {day.schedule ? <p>Schedule: {day.schedule}</p> : null}
          {editable && !form ? <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="button" disabled={busy} className={styles.secondaryButton} onClick={() => openForm(day)}>Edit Day {day.dayNumber}</button>
            <button type="button" disabled={busy} className={styles.dangerButton} onClick={() => { setError(null); setDeleting(day) }}>Delete Day {day.dayNumber}</button>
          </div> : null}
        </article>)}
      </div>
      {editable && !form ? <button type="button" disabled={busy} className={styles.primaryButton} style={{ marginTop: 16 }} onClick={() => openForm()}>Add itinerary day</button> : null}
      {editable && form ? <form onSubmit={save} style={{ display: 'grid', gap: 16, marginTop: 20 }}>
        <h4>{form.id ? `Edit Day ${form.dayNumber}` : 'Add itinerary day'}</h4>
        {!form.id ? <div className={styles.formGroup}><label htmlFor={`${prefix}-number`}>Day number</label>
          <input id={`${prefix}-number`} className={styles.formInput} type="number" min="1" step="1" required disabled={busy} value={form.dayNumber} onChange={(event) => setForm({ ...form, dayNumber: event.target.value })} /></div> : null}
        <div className={styles.formGroup}><label htmlFor={`${prefix}-title`}>Day title</label>
          <input id={`${prefix}-title`} className={styles.formInput} maxLength={200} required disabled={busy} value={form.title} placeholder="Arrival and tea garden visit" onChange={(event) => setForm({ ...form, title: event.target.value })} /></div>
        <div className={styles.formGroup}><label htmlFor={`${prefix}-activities`}>Planned activities</label>
          <textarea id={`${prefix}-activities`} className={styles.formTextarea} maxLength={2000} rows={4} required disabled={busy} value={form.activities} placeholder="Hotel check-in, lunch, and a guided tea garden visit" onChange={(event) => setForm({ ...form, activities: event.target.value })} /></div>
        <div className={styles.formGroup}><label htmlFor={`${prefix}-schedule`}>Schedule (optional)</label>
          <input id={`${prefix}-schedule`} className={styles.formInput} maxLength={100} disabled={busy} value={form.schedule ?? ''} placeholder="10:00 arrival; 14:00 tea garden" onChange={(event) => setForm({ ...form, schedule: event.target.value })} /></div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button type="submit" className={styles.primaryButton} disabled={busy}>{busy ? 'Saving…' : 'Save itinerary day'}</button>
          <button type="button" className={styles.secondaryButton} disabled={busy} onClick={() => { setForm(null); setError(null) }}>Cancel</button>
        </div>
      </form> : null}
    </>}
    <ConfirmDialog open={Boolean(deleting)} busy={busy} title={`Delete Day ${deleting?.dayNumber}?`} description="This removes the day and all its tracked activities. This cannot be undone." confirmLabel="Delete day" tone="danger" onCancel={() => setDeleting(null)} onConfirm={remove} />
  </section>
}
