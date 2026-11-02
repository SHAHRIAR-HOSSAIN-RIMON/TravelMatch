import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../components/tourist/PageHeading.jsx'
import FeedbackBanner from '../components/tourist/FeedbackBanner.jsx'
import { useAuth } from '../context/useAuth.js'
import { api, describeApiError } from '../lib/api.js'
import { mergeMessages, normalizeChatHistoryPayload } from '../lib/organizedTripChatModel.js'
import { buildHubConnection } from '../lib/signalrConnection.js'
import styles from '../components/tourist/Tourist.module.css'
import chatStyles from './OrganizedTripChatPage.module.css'

function Chat({ tripId, token, logout, user }) {
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const [hasMore, setHasMore] = useState(false)
  const [connected, setConnected] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const active = useRef(false)
  const sendLock = useRef(false)
  const initialized = useRef(false)
  const refresh = useRef(() => {})
  const list = useRef(null)
  const followLatest = useRef(true)
  const path = `/api/organized-trips/${tripId}/chat`

  useEffect(() => {
    let cancelled = false
    let running = false
    let queued = false
    active.current = true
    async function load() {
      if (running) { queued = true; return }
      running = true
      try {
        const result = await api.get(path, { token })
        if (cancelled) return

        const normalized = normalizeChatHistoryPayload(result)
        if (!normalized) {
          throw new Error('The chat server returned an unexpected response.')
        }

        setData((previous) => ({
          ...normalized,
          messages: mergeMessages(previous?.messages ?? [], normalized.messages),
        }))
        if (!initialized.current) { setHasMore(normalized.hasMore); initialized.current = true }
        setError(null)
      } catch (err) {
        if (cancelled) return
        setError(describeApiError(err, 'Chat could not be loaded.'))
        if (err.status === 403 || err.status === 401) { setData(null); setHasMore(false); initialized.current = false }
        if (err.status === 401) logout()
      } finally {
        running = false
        if (queued && !cancelled) { queued = false; void load() }
      }
    }
    refresh.current = load
    const connection = buildHubConnection('/hubs/organized-trip-chat', token)
    connection.on('ChatUpdated', (id) => { if (Number(id) === tripId) void load() })
    connection.onreconnecting(() => { if (!cancelled) setConnected(false) })
    connection.onreconnected(() => { if (!cancelled) { setConnected(true); void load() } })
    connection.onclose(() => { if (!cancelled) setConnected(false) })
    connection.start().then(() => { if (!cancelled) { setConnected(true); void load() } }).catch(() => { if (!cancelled) setConnected(false) })
    void load()
    // Also revalidate membership and lifecycle when no messages are being sent.
    const timer = setInterval(() => { if (document.visibilityState === 'visible') void load() }, 5000)
    const visible = () => { if (document.visibilityState === 'visible') void load() }
    document.addEventListener('visibilitychange', visible)
    return () => {
      cancelled = true
      active.current = false
      clearInterval(timer)
      document.removeEventListener('visibilitychange', visible)
      void connection.stop()
    }
  }, [path, token, tripId, logout, attempt])

  const latest = data?.messages.at(-1)?.id
  useEffect(() => {
    if (followLatest.current && list.current) list.current.scrollTop = list.current.scrollHeight
  }, [latest])

  async function older() {
    if (loadingOlder || !data?.messages.length) return
    setLoadingOlder(true)
    try {
      const result = await api.get(`${path}?beforeId=${data.messages[0].id}`, { token })
      if (!active.current) return

      const normalized = normalizeChatHistoryPayload(result)
      if (!normalized) {
        throw new Error('The chat server returned an unexpected response.')
      }

      followLatest.current = false
      setData((previous) => previous ? { ...previous, messages: mergeMessages(normalized.messages, previous.messages) } : previous)
      setHasMore(normalized.hasMore)
    } catch (err) {
      if (!active.current) return
      setError(describeApiError(err))
      if (err.status === 403 || err.status === 401) setData(null)
      if (err.status === 401) logout()
    } finally { if (active.current) setLoadingOlder(false) }
  }

  async function send(event) {
    event.preventDefault()
    if (sendLock.current || !draft.trim() || data?.readOnly) return
    sendLock.current = true
    setSending(true)
    try {
      await api.post(path, { content: draft.trim() }, { token })
      if (!active.current) return
      setDraft('')
      followLatest.current = true
      refresh.current()
    } catch (err) {
      if (!active.current) return
      setError(describeApiError(err, 'Message could not be sent.'))
      if (err.status === 403 || err.status === 401) setData(null)
      if (err.status === 401) logout()
      if (err.status === 409) setData((previous) => previous ? { ...previous, readOnly: true } : previous)
    } finally { sendLock.current = false; if (active.current) setSending(false) }
  }

  const backTo = user.role === 'Organizer' ? `/organizer/trips/${tripId}` : user.role === 'Guide' ? `/guide/organized-trips/${tripId}` : '/registrations'
  return <div className={styles.container}>
    <PageHeading title={data ? `${data.title} — group chat` : 'Trip group chat'} backTo={backTo} backLabel="Back to trip" description="A shared conversation for the organizer, selected guide and registered tourists." />
    <FeedbackBanner tone="error">{error}</FeedbackBanner>
    <div className={styles.formActions}>
      <span role="status">{connected ? 'Live updates connected' : 'Refreshing every 5 seconds'}</span>
      <button className={styles.secondaryButton} type="button" onClick={() => setAttempt((n) => n + 1)}>Refresh / reconnect</button>
    </div>
    {!data && !error && <p role="status">Loading conversation…</p>}
    {data && <>
      <details className={styles.panel}><summary>Participants ({data.members.length})</summary>
        <ul>{data.members.map((member) => <li key={member.id}>{member.name} · {member.role}</li>)}</ul>
      </details>
      <section className={styles.panel} aria-label="Trip conversation">
        {data.readOnly && <FeedbackBanner>This trip has ended. You can read previous messages.</FeedbackBanner>}
        {hasMore && <button className={styles.secondaryButton} disabled={loadingOlder} type="button" onClick={older}>{loadingOlder ? 'Loading…' : 'Load older messages'}</button>}
        <div className={chatStyles.messages} ref={list} onScroll={() => { const el = list.current; followLatest.current = el.scrollHeight - el.scrollTop - el.clientHeight < 60 }}>
          {!data.messages.length && <p>No messages yet. Start the conversation below.</p>}
          {data.messages.map((message) => <article className={chatStyles.message} key={message.id}>
            <strong>{message.senderName} · {message.senderRole}</strong>
            <time className={chatStyles.time} dateTime={message.sentAt}>{new Date(message.sentAt).toLocaleString()}</time>
            <p className={chatStyles.content}>{message.content}</p>
          </article>)}
        </div>
        {!data.readOnly && <form className={styles.form} onSubmit={send}>
          <label className={styles.field}>Message the group
            <textarea className={styles.textarea} value={draft} onChange={(e) => setDraft(e.target.value)} maxLength={2000} required disabled={sending} />
          </label>
          <button className={styles.primaryButton} type="submit" disabled={sending || !draft.trim()}>{sending ? 'Sending…' : 'Send message'}</button>
        </form>}
      </section>
    </>}
  </div>
}

export default function OrganizedTripChatPage() {
  const { tripId } = useParams()
  const { user, token, logout } = useAuth()
  const id = Number(tripId)
  return <DashboardLayout user={user} onLogout={logout}>
    {Number.isSafeInteger(id) && id > 0 ? <Chat key={`${id}:${user.id}`} tripId={id} user={user} token={token} logout={logout} /> : <p>Invalid trip link.</p>}
  </DashboardLayout>
}
