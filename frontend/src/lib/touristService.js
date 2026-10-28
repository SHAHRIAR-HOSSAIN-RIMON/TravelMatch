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

function unwrapList(response) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'The request could not be completed.')
  }

  return Array.isArray(envelope.data) ? envelope.data : []
}

function unwrapData(response, fallbackMessage) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || fallbackMessage)
  }

  return envelope.data ?? response
}

/** Route suffixes accepted by api/trip-requests/{id}/lifecycle. */
const LIFECYCLE_TRANSITIONS = [
  'proposals-received',
  'matched',
  'payment-pending',
  'planned',
  'in-progress',
  'completed',
  'cancelled',
]

// ── Trip requests ────────────────────────────────────────────────────────────

/**
 * Trip requests owned by the authenticated tourist.
 * Backend: GET /api/TripRequests (Tourist only, newest first).
 */
export async function getMyTripRequests(token) {
  return unwrapList(await api.get('/api/TripRequests', { token }))
}

/**
 * A single trip request owned by the authenticated tourist.
 * Backend: GET /api/TripRequests/{id} (Tourist only, 403 when not the owner).
 */
export async function getTripRequest(token, tripRequestId) {
  const response = await api.get(`/api/TripRequests/${tripRequestId}`, { token })
  return unwrapData(response, 'The trip request could not be loaded.')
}

/**
 * Replace the editable fields of a Pending trip request.
 * Backend: PUT /api/TripRequests/{id}. The service rejects updates once the request
 * has left Pending, answering 403 (TripRequestError.Unauthorized).
 */
export async function updateTripRequest(token, tripRequestId, payload) {
  const response = await api.put(`/api/TripRequests/${tripRequestId}`, payload, { token })
  return unwrapData(response, 'The trip request could not be updated.')
}

/**
 * Lifecycle state of a trip request, including `allowedTransitions`.
 * Backend: GET /api/trip-requests/{id}/lifecycle (Tourist only).
 */
export async function getTripRequestLifecycle(token, tripRequestId) {
  const response = await api.get(`/api/trip-requests/${tripRequestId}/lifecycle`, { token })
  return unwrapData(response, 'The trip status could not be loaded.')
}

/**
 * Advance a trip request through its lifecycle.
 * Backend: POST /api/trip-requests/{id}/lifecycle/{transition}.
 * Only the transitions below exist; anything else would be an invented endpoint.
 *
 * `payload` must carry `proposalId` for `matched`, `acceptedPrice` for `payment-pending`
 * and optionally `cancellationReason` for `cancelled`.
 */
export async function transitionTripRequest(token, tripRequestId, transition, payload = {}) {
  if (!LIFECYCLE_TRANSITIONS.includes(transition)) {
    throw new Error(`Unsupported trip transition: ${transition}`)
  }

  const response = await api.post(
    `/api/trip-requests/${tripRequestId}/lifecycle/${transition}`,
    payload,
    { token },
  )

  return unwrapData(response, 'The trip status could not be changed.')
}

// ── Proposals ────────────────────────────────────────────────────────────────

/**
 * Submitted proposals for one trip request owned by the authenticated tourist.
 * Backend: GET /api/trip-requests/{tripRequestId}/proposals (Tourist only).
 */
export async function getTripRequestProposals(token, tripRequestId) {
  return unwrapList(await api.get(`/api/trip-requests/${tripRequestId}/proposals`, { token }))
}

/**
 * Full proposal detail, including `isEligibleForAcceptance` and the itinerary.
 * Backend: GET /api/trip-requests/{tripRequestId}/proposals/{proposalId}.
 */
export async function getProposalDetail(token, tripRequestId, proposalId) {
  const response = await api.get(
    `/api/trip-requests/${tripRequestId}/proposals/${proposalId}`,
    { token },
  )

  return unwrapData(response, 'The proposal could not be loaded.')
}

/**
 * Accept a submitted proposal. The backend moves the trip to Matched and rejects the
 * other submitted proposals; there is no tourist endpoint for rejecting a proposal.
 * Backend: POST /api/trip-requests/{tripRequestId}/proposals/{proposalId}/accept.
 */
export async function acceptProposal(token, tripRequestId, proposalId) {
  const response = await api.post(
    `/api/trip-requests/${tripRequestId}/proposals/${proposalId}/accept`,
    undefined,
    { token },
  )

  return unwrapData(response, 'The proposal could not be accepted.')
}

// ── Payments and Escrow (Delegated to Unified paymentService) ───────────────

export {
  getMyPayments,
  payForProposal,
  payForRegistration,
  releaseEscrow,
  releaseMilestone,
} from './paymentService.js'

// ── Organized trips and registrations ────────────────────────────────────────

/**
 * Organized trips currently open for registration with spots remaining.
 * Backend: GET /api/OrganizedTrips ([AllowAnonymous]).
 */
export async function getAvailableOrganizedTrips(token) {
  return unwrapList(await api.get('/api/OrganizedTrips', { token }))
}

/**
 * Detail of one organized trip that is open for registration.
 * Backend: GET /api/OrganizedTrips/{organizedTripId} ([AllowAnonymous], 404 when closed).
 */
export async function getAvailableOrganizedTrip(token, organizedTripId) {
  const response = await api.get(`/api/OrganizedTrips/${organizedTripId}`, { token })
  return unwrapData(response, 'The organized trip could not be loaded.')
}

/**
 * Register the authenticated tourist for an organized trip.
 * Backend: POST /api/organized-trips/{organizedTripId}/registrations (Tourist only).
 * Capacity, deadline and duplicate rules are enforced server-side.
 */
export async function registerForOrganizedTrip(token, organizedTripId, payload) {
  const response = await api.post(
    `/api/organized-trips/${organizedTripId}/registrations`,
    payload,
    { token },
  )

  return unwrapData(response, 'The registration could not be created.')
}

/**
 * All registrations owned by the authenticated tourist.
 * Backend: GET /api/organized-trips/{organizedTripId}/registrations/my.
 * The action takes no route value — `GetMyRegistrations()` ignores the trip id — but the
 * route template still requires an integer segment, so 0 is used as a placeholder.
 */
export async function getMyRegistrations(token) {
  return unwrapList(await api.get('/api/organized-trips/0/registrations/my', { token }))
}

/**
 * Cancel a registration owned by the authenticated tourist.
 * Backend: POST /api/organized-trips/{organizedTripId}/registrations/{registrationId}/cancel.
 */
export async function cancelRegistration(token, organizedTripId, registrationId, cancellationReason) {
  const response = await api.post(
    `/api/organized-trips/${organizedTripId}/registrations/${registrationId}/cancel`,
    { cancellationReason: cancellationReason || null },
    { token },
  )

  return unwrapData(response, 'The registration could not be cancelled.')
}

// ── Cost breakdown (Delegated to Unified paymentService) ──────────────────────

export {
  getProposalCostBreakdown,
  getRegistrationCostBreakdown,
  estimateOrganizedTripCost as getOrganizedTripCostEstimate,
} from './paymentService.js'

// ── Creation ─────────────────────────────────────────────────────────────────

/**
 * Create a new custom trip request owned by the authenticated tourist.
 * Backend: POST /api/TripRequests (Tourist only).
 */
export async function createTripRequest(token, payload) {
  const response = await api.post('/api/TripRequests', payload, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'The trip request could not be created.')
  }

  return envelope.data ?? response
}
