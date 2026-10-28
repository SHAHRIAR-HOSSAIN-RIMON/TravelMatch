import { api } from './api.js'

function normalizeEnvelope(response) {
  if (!response || typeof response !== 'object') {
    return { success: false, data: null, message: '' }
  }

  return {
    success: response.success ?? response.Success ?? true,
    data: response.data ?? response.Data ?? null,
    message: response.message ?? response.Message ?? '',
  }
}

function unwrapData(response, fallbackMessage) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || fallbackMessage)
  }

  return envelope.data ?? response
}

/**
 * Raise a dispute for a trip request or organized trip.
 * Available to participants: Tourist, Guide, Organizer.
 * Backend: POST /api/Disputes (Authenticated).
 * Body: { tripType: 'TripRequest' | 'OrganizedTrip', tripId: number, reason: string, description: string }
 */
export async function raiseDispute(token, { tripType, tripId, reason, description }) {
  const payload = {
    tripType,
    tripId: Number(tripId),
    reason: reason?.trim() || '',
    description: description?.trim() || '',
  }

  const response = await api.post('/api/Disputes', payload, { token })
  return unwrapData(response, 'The dispute could not be submitted.')
}

/**
 * Administrator-only listing of all disputes, optionally filtered by status.
 * Backend: GET /api/Disputes?status= (Admin only).
 * Status options: 'Open', 'Resolved', 'Closed'.
 */
export async function getDisputes(token, status = null) {
  const query = status && status !== 'All' ? `?status=${encodeURIComponent(status)}` : ''
  const response = await api.get(`/api/Disputes${query}`, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load disputes.')
  }

  return {
    success: envelope.success,
    data: Array.isArray(envelope.data) ? envelope.data : [],
    message: envelope.message,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
  }
}

/**
 * Administrator-only retrieval of a single dispute by ID.
 * Backend: GET /api/Disputes/{disputeId} (Admin only).
 */
export async function getDisputeById(token, disputeId) {
  const response = await api.get(`/api/Disputes/${disputeId}`, { token })
  return unwrapData(response, 'Could not load dispute details.')
}

/**
 * Administrator-only resolution of an open dispute.
 * Backend: POST /api/Disputes/{disputeId}/resolve (Admin only).
 * Body: { resolutionNote: string }
 */
export async function resolveDispute(token, disputeId, resolutionNote) {
  const payload = {
    resolutionNote: resolutionNote?.trim() || '',
  }

  const response = await api.post(`/api/Disputes/${disputeId}/resolve`, payload, { token })
  return unwrapData(response, 'The dispute could not be resolved.')
}

