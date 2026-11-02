import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import { buildHubConnection } from './signalrConnection.js'
import { useAuth } from '../context/useAuth.js'
import { ApiError, describeApiError } from './api.js'

/** Join before resyncing, and replay pushes received during a REST snapshot. */
export function useProposalLiveData({ token, proposalId, hubPath, joinMethod, leaveMethod, load, reduce, events, readOnly = false, initialData = [] }) {
  const { logout } = useAuth()
  const [restricted, setRestricted] = useState(false)
  const [data, setData] = useState(initialData)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [connectionState, setConnectionState] = useState(HubConnectionState.Disconnected)
  const [connectionError, setConnectionError] = useState(null)
  const actions = useRef(null)

  useEffect(() => {
    let disposed = false
    const abort = new AbortController()
    let retryTimer
    let syncing = false
    let resyncRequested = false
    let pending = []
    let retries = 0
    let joined = false
    let accessDenied = false
    let joinError = null
    const connection = buildHubConnection(hubPath, token)

    function apply(event, payload) {
      if (disposed) return
      if (syncing) pending.push([event, payload])
      setData((previous) => reduce(previous, event, payload))
    }

    async function refresh() {
      if (disposed) return
      if (syncing) { resyncRequested = true; return }
      syncing = true
      pending = []
      try {
        const snapshot = await load(token, proposalId, { signal: abort.signal })
        if (disposed) return
        const received = pending.slice()
        setData((previous) => received.reduce(
          (current, [event, payload]) => reduce(current, event, payload),
          reduce(previous, 'snapshot', snapshot),
        ))
        setError(null)
      } catch (err) {
        if (!disposed && err instanceof ApiError && err.status === 401) { logout(); return }
        if (!disposed) setError(describeApiError(err, 'Could not refresh. Please try again.'))
      } finally {
        syncing = false
        if (!disposed) {
          setLoading(false)
          if (resyncRequested) { resyncRequested = false; void refresh() }
        }
      }
    }

    async function join() {
      joined = false
      await connection.invoke(joinMethod, proposalId)
      if (!disposed && !joined) throw new Error(joinError || 'Could not join this trip. Check your access and retry.')
    }

    function scheduleRetry() {
      if (disposed || readOnly || accessDenied) return
      clearTimeout(retryTimer)
      retryTimer = setTimeout(start, Math.min(30_000, 2_000 * 2 ** Math.min(retries++, 4)))
    }

    async function start() {
      if (disposed) return
      try {
        setConnectionState(HubConnectionState.Connecting)
        await connection.start()
        if (disposed) { await connection.stop(); return }
        await join()
        retries = 0
      } catch (err) {
        if (disposed) return
        setConnectionError(describeApiError(err, 'Live updates unavailable. Retrying…'))
        setConnectionState(HubConnectionState.Disconnected)
        await connection.stop().catch(() => {})
        scheduleRetry()
      }
    }

    events.forEach((event) => connection.on(event, (payload) => apply(event, payload)))
    connection.on('Error', (message) => {
      if (disposed) return
      joined = false
      accessDenied = true
      joinError = message
      setRestricted(true)
      setConnectionError(message || 'Unable to access live updates.')
      setConnectionState(HubConnectionState.Disconnected)
    })
    connection.on('JoinedGroup', (group) => {
      if (disposed || group !== `proposal-${proposalId}`) return
      joined = true
      setConnectionState(HubConnectionState.Connected)
      setConnectionError(null)
      void refresh()
    })
    connection.onreconnecting(() => {
      if (!disposed) {
        setConnectionState(HubConnectionState.Reconnecting)
        setConnectionError('Connection interrupted. Reconnecting…')
      }
    })
    connection.onreconnected(async () => {
      if (disposed) return
      try { await join() } catch (err) {
        if (!disposed) {
          setConnectionError(describeApiError(err))
          await connection.stop().catch(() => {})
          scheduleRetry()
        }
      }
    })
    connection.onclose(() => {
      if (!disposed) {
        setConnectionState(HubConnectionState.Disconnected)
        scheduleRetry()
      }
    })
    actions.current = { refresh, apply }
    void refresh()
    if (!readOnly) void start()
    // REST recovery also picks up itinerary edits for which the backend has no event.
    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') void refresh()
    }, 30_000)
    const onVisible = () => { if (document.visibilityState === 'visible') void refresh() }
    document.addEventListener('visibilitychange', onVisible)
    window.addEventListener('online', onVisible)
    return () => {
      disposed = true
      abort.abort()
      clearTimeout(retryTimer)
      clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisible)
      window.removeEventListener('online', onVisible)
      actions.current = null
      if (connection.state === HubConnectionState.Connected) {
        void connection.invoke(leaveMethod, proposalId).catch(() => {})
      }
      void connection.stop().catch(() => {})
    }
  }, [token, proposalId, hubPath, joinMethod, leaveMethod, load, reduce, events, readOnly, logout])

  const refresh = useCallback(() => actions.current?.refresh(), [])
  const apply = useCallback((event, payload) => actions.current?.apply(event, payload), [])
  return { data, loading, error, connectionState, connectionError, restricted, refresh, apply }
}
