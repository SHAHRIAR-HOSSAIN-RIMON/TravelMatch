import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from './api.js'

export const LOADING = 'loading'
export const READY = 'ready'
export const ERROR = 'error'

/**
 * Loads one tourist resource and exposes its status, data and error.
 *
 * `loader` must be a stable reference (wrap it in useCallback), otherwise the
 * effect re-runs on every render. An expired JWT (401) is handed to
 * `onUnauthorized` so the page logs out through AuthContext — the same pattern
 * the dashboard uses. While a reload is in flight the previous data is kept, so
 * pages that re-query (for example the traveler-count estimate) do not flash.
 */
export function useResource(loader, onUnauthorized, refreshInterval = 0) {
  const [state, setState] = useState({ status: LOADING, data: [], error: null })
  const [attempt, setAttempt] = useState(0)
  const onUnauthorizedRef = useRef(onUnauthorized)

  useEffect(() => {
    onUnauthorizedRef.current = onUnauthorized
  }, [onUnauthorized])

  useEffect(() => {
    let cancelled = false

    let inFlight = false
    async function run(background = false) {
      if (inFlight) return
      inFlight = true
      try {
        const data = await loader()
        if (cancelled) return

        setState({ status: READY, data, error: null, refreshError: null })
      } catch (error) {
        if (cancelled) return

        if (error instanceof ApiError && error.status === 401) {
          onUnauthorizedRef.current?.()
          return
        }

        if (!background) setState({ status: ERROR, data: null, error })
        else setState((previous) => ({ ...previous, refreshError: error }))
      } finally {
        inFlight = false
      }
    }

    run()
    const refresh = () => { if (document.visibilityState === 'visible') void run(true) }
    const timer = refreshInterval ? setInterval(refresh, refreshInterval) : null
    if (refreshInterval) document.addEventListener('visibilitychange', refresh)

    return () => {
      cancelled = true
      if (timer) clearInterval(timer)
      if (refreshInterval) document.removeEventListener('visibilitychange', refresh)
    }
  }, [attempt, loader, refreshInterval])

  const reload = useCallback(() => {
    setState({ status: LOADING, data: [], error: null })
    setAttempt((current) => current + 1)
  }, [])

  return { ...state, reload }
}
