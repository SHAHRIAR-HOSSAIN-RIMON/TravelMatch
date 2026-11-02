import { useEffect, useRef, useState } from 'react'
import { HUB_PATHS } from '../../lib/signalrConnection.js'
import { getMessageHistory, markMessageRead, sendMessageHttp } from '../../lib/messagingService.js'
import { describeApiError } from '../../lib/api.js'
import { useProposalLiveData } from '../../lib/useProposalLiveData.js'
import { CHAT_EVENTS, EMPTY_CHAT, reduceChat } from '../../lib/proposalLiveModel.js'
import ConnectionPill from './ConnectionPill.jsx'
import styles from './Messaging.module.css'

export default function TripChatPanel({ proposalId, token, currentUserId, readOnly = false }) {
  const { data, loading, error, connectionState, connectionError, restricted, refresh, apply } = useProposalLiveData({
    proposalId, token, hubPath: HUB_PATHS.tripChat, joinMethod: 'JoinTripChat',
    leaveMethod: 'LeaveTripChat', load: getMessageHistory, reduce: reduceChat,
    events: CHAT_EVENTS, initialData: EMPTY_CHAT, readOnly,
  })
  const historyOnly = readOnly || restricted
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [actionError, setActionError] = useState(null)
  const panel = useRef(null)
  const [visible, setVisible] = useState(false)
  const list = useRef(null)
  const followLatest = useRef(true)
  const receipts = useRef(new Set())
  const active = useRef(false)
  const sendLock = useRef(false)

  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), { threshold: 0.1 })
    observer.observe(panel.current)
    return () => observer.disconnect()
  }, [])

  const latestId = data.messages.at(-1)?.id
  useEffect(() => {
    if (followLatest.current && list.current) list.current.scrollTo({ top: list.current.scrollHeight, behavior: 'smooth' })
  }, [latestId])

  useEffect(() => {
    function readVisibleMessages() {
      if (!visible || document.visibilityState !== 'visible') return
      data.messages.filter((m) => !m.isRead && Number(m.senderId) !== Number(currentUserId) && !receipts.current.has(m.id))
        .forEach((message) => {
          receipts.current.add(message.id)
          markMessageRead(token, proposalId, message.id).then((updated) => {
            if (active.current) apply('MessageRead', updated)
          }).catch((err) => {
            receipts.current.delete(message.id)
            if (active.current) setActionError(describeApiError(err, 'Could not update read receipts.'))
          })
        })
    }
    readVisibleMessages()
    document.addEventListener('visibilitychange', readVisibleMessages)
    return () => document.removeEventListener('visibilitychange', readVisibleMessages)
  }, [data.messages, token, proposalId, currentUserId, apply, visible])

  async function send(event) {
    event.preventDefault()
    const content = draft.trim()
    if (!content || sendLock.current || historyOnly) return
    sendLock.current = true
    setSending(true)
    setActionError(null)
    try {
      // The REST endpoint persists and broadcasts ReceiveMessage through TripChatHub.
      // Its explicit result preserves the draft on rejection or failed persistence.
      const message = await sendMessageHttp(token, proposalId, content)
      if (!active.current) return
      followLatest.current = true
      apply('MessageSent', message)
      setDraft('')
    } catch (err) {
      if (active.current) setActionError(describeApiError(err, 'Message could not be sent.'))
    } finally {
      sendLock.current = false
      if (active.current) setSending(false)
    }
  }

  return (
    <section className={styles.panel} aria-label="Trip chat" ref={panel}>
      <div className={styles.panelHeader}>
        <h2 className={styles.panelTitle}>Trip Chat</h2>
        <ConnectionPill state={connectionState} readOnly={historyOnly} />
      </div>
      <p className={styles.panelLead}>Message your trip partner. Your conversation is saved for this trip.</p>
      {connectionError ? <p className={styles.inlineError} role="status">{connectionError}</p> : null}
      {error ? <div className={styles.inlineError} role="alert">{error} <button type="button" onClick={refresh}>Retry history</button></div> : null}
      <div className={styles.messageList} ref={list} role="log" aria-label="Messages" aria-live="polite" aria-busy={loading}
        onScroll={() => { const el = list.current; followLatest.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80 }}>
        {loading ? <p>Loading messages…</p> : !data.messages.length ? <p className={styles.emptyChat}>No messages yet.</p> : null}
        {data.messages.map((message) => {
          const own = Number(message.senderId) === Number(currentUserId)
          // Backend DateTime fields represent UTC but may omit the zone suffix.
          const date = new Date(/(?:Z|[+-]\d\d:\d\d)$/i.test(message.sentAt) ? message.sentAt : `${message.sentAt}Z`)
          return <div key={message.id} className={`${styles.bubble} ${own ? styles.bubbleOwn : styles.bubbleOther}`}>
            {!own ? <span className={styles.bubbleSender}>{message.senderName} · {message.senderRole}</span> : null}
            <p className={styles.bubbleText}>{message.content}</p>
            <span className={styles.bubbleMeta}><time dateTime={date.toISOString()}>{date.toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</time>{own ? message.isRead ? ' · Read' : ' · Sent' : ''}</span>
          </div>
        })}
      </div>
      {actionError ? <p className={styles.inlineError} role="alert">{actionError}</p> : null}
      {historyOnly ? <p className={styles.panelLead}>Conversation history is read only for this trip.</p> : <form className={styles.compose} onSubmit={send}>
        <textarea aria-label="Message" className={styles.composeInput} maxLength={2000} rows={2} value={draft} disabled={sending}
          onChange={(e) => setDraft(e.target.value)} placeholder="Type a message…"
          onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) { e.preventDefault(); e.currentTarget.form.requestSubmit() } }} />
        <button className={styles.sendButton} disabled={sending || !draft.trim()} type="submit">{sending ? 'Sending…' : 'Send'}</button>
      </form>}
      {!historyOnly ? <p className={styles.hint}>{draft.length} / 2000 · Enter to send · Shift+Enter for new line</p> : null}
    </section>
  )
}
