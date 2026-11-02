/**
 * Messaging REST service — Phase 6
 *
 * Wraps the HTTP endpoints on TripMessagesController.
 * Real-time delivery is handled by TripChatHub (signalrConnection.js).
 * These calls are used for:
 *   1. Loading message history before the SignalR connection joins.
 *   2. HTTP fallback send (when SignalR is unavailable).
 *   3. Marking messages as read.
 *
 * Backend routes (TripMessagesController):
 *   GET  /api/trip-messages/proposals/{proposalId}
 *   POST /api/trip-messages/proposals/{proposalId}
 *   POST /api/trip-messages/proposals/{proposalId}/messages/{messageId}/read
 */

import { api } from './api.js'
import { loadMessagePages } from './messageHistory.js'

function normalizeEnvelope(response) {
  if (!response || typeof response !== 'object') return { success: false, data: null, message: '' }
  return {
    success: response.success ?? response.Success ?? true,
    data: response.data ?? response.Data ?? null,
    message: response.message ?? response.Message ?? '',
  }
}

function unwrapList(response) {
  const env = normalizeEnvelope(response)
  if (env.success === false) throw new Error(env.message || 'Failed to load messages.')
  return {
    messages: Array.isArray(env.data) ? env.data : [],
    totalCount: response?.totalCount ?? response?.TotalCount ?? 0,
    hasMore: response?.hasMore ?? response?.HasMore ?? false,
    page: response?.page ?? 1,
  }
}

function unwrapData(response, fallback) {
  const env = normalizeEnvelope(response)
  if (env.success === false) throw new Error(env.message || fallback)
  return env.data ?? response
}

/**
 * Load paginated message history for a proposal.
 * Returns { messages: TripMessageDto[], totalCount, hasMore, page }
 *
 * @param {string} token
 * @param {number} proposalId
 * @param {object} [opts]
 * @param {number} [opts.page=1]
 * @param {number} [opts.pageSize=50]
 * @param {number} [opts.beforeId]  cursor — load messages with id < beforeId
 */
export async function getMessages(token, proposalId, opts = {}) {
  const { page = 1, pageSize = 50, beforeId, signal } = opts
  const params = new URLSearchParams({ page, pageSize })
  if (beforeId) params.set('beforeId', beforeId)

  const response = await api.get(
    `/api/trip-messages/proposals/${proposalId}?${params}`,
    { token, signal },
  )
  return unwrapList(response)
}

/**
 * HTTP fallback — send a message without SignalR.
 * The controller also broadcasts via IHubContext so recipients still receive
 * the message in real time even when sent over REST.
 *
 * @param {string} token
 * @param {number} proposalId
 * @param {string} content
 * @returns {Promise<TripMessageDto>}
 */
export async function sendMessageHttp(token, proposalId, content) {
  const response = await api.post(
    `/api/trip-messages/proposals/${proposalId}`,
    { proposalId, content },
    { token },
  )
  return unwrapData(response, 'The message could not be sent.')
}

/**
 * Mark a message as read.
 * @param {string} token
 * @param {number} proposalId
 * @param {number} messageId
 */
export async function markMessageRead(token, proposalId, messageId) {
  const response = await api.post(
    `/api/trip-messages/proposals/${proposalId}/messages/${messageId}/read`,
    undefined,
    { token },
  )
  return unwrapData(response, 'Could not mark message as read.')
}

/** Load the complete saved conversation using the server's pagination. */
export async function getMessageHistory(token, proposalId, { signal } = {}) {
  return loadMessagePages((page) => getMessages(token, proposalId, { page, pageSize: 100, signal }))
}
