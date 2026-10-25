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
export function useResource(loader, onUnauthorized) {
  const [state, setState] = useState({ status: LOADING, data: null, error: null })
  const [attempt, setAttempt] = useState(0)
  const onUnauthorizedRef = useRef(onUnauthorized)

  useEffect(() => {
    onUnauthorizedRef.current = onUnauthorized
  }, [onUnauthorized])

  useEffect(() => {
    let cancelled = false

    async function run() {
      try {
        const data = await loader()
        if (cancelled) return

        setState({ status: READY, data, error: null })
      } catch (error) {
        if (cancelled) return

        if (error instanceof ApiError && error.status === 401) {
          onUnauthorizedRef.current?.()
          return
        }

        setState({ status: ERROR, data: null, error })
      }
    }

    run()

    return () => {
      cancelled = true
    }
  }, [attempt, loader])

  const reload = useCallback(() => {
    setState({ status: LOADING, data: null, error: null })
    setAttempt((current) => current + 1)
  }, [])

  return { ...state, reload }
}
