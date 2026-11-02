/** Authenticated SignalR connections share the REST API base URL. */

import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/** Reconnect delays: 0 s, 2 s, 5 s, 10 s, 30 s, then every 30 s */
const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000]

const retryPolicy = {
  nextRetryDelayInMilliseconds(context) {
    const idx = Math.min(context.previousRetryCount, RETRY_DELAYS_MS.length - 1)
    return RETRY_DELAYS_MS[idx]
  },
}

/**
 * @param {string} hubPath  e.g. '/hubs/trip-chat'
 * @param {string} token    JWT — passed as ?access_token= so the backend can
 *                          read it from the query string in OnMessageReceived
 * @returns {import('@microsoft/signalr').HubConnection}
 */
export function buildHubConnection(hubPath, token) {
  const url = `${API_BASE_URL}${hubPath}`

  return new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => token,
      // Prefer WebSockets; fall back to SSE then long-polling so the panel
      // still works behind proxies that block WebSocket upgrades.
      transport:
        HttpTransportType.WebSockets |
        HttpTransportType.ServerSentEvents |
        HttpTransportType.LongPolling,
      skipNegotiation: false,
    })
    .withAutomaticReconnect(retryPolicy)
    .configureLogging(
      import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning,
    )
    .build()
}

/** Hub path constants so every consumer spells them the same way. */
export const HUB_PATHS = {
  tripChat: '/hubs/trip-chat',
  itineraryTracking: '/hubs/itinerary-tracking',
  notifications: '/hubs/notifications',
}
