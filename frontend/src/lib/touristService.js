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

// ── Payments owned by the tourist ────────────────────────────────────────────

/**
 * Payment (escrow) records owned by the authenticated tourist.
 * Backend: GET /api/Payments/my (Tourist only). Enum fields arrive as numbers.
 */
export async function getMyPayments(token) {
  return unwrapList(await api.get('/api/Payments/my', { token }))
}

/**
 * Pay the simulated escrow deposit for an accepted proposal.
 * Funds are held in escrow (PaymentStatus.Held).
 * Backend: POST /api/Payments/proposal/{proposalId} (Tourist only).
 * Optional body: { paymentMethod?, milestones? }
 */
export async function payForProposal(token, proposalId, payload = {}) {
  const response = await api.post(`/api/Payments/proposal/${proposalId}`, payload, { token })
  return unwrapData(response, 'The deposit could not be paid.')
}

/**
 * Pay the simulated escrow deposit for a registration. The backend confirms the
 * registration on success; a declined sandbox payment answers 402.
 * Backend: POST /api/Payments/registration/{registrationId} (Tourist only).
 */
export async function payForRegistration(token, registrationId) {
  const response = await api.post(`/api/Payments/registration/${registrationId}`, {}, { token })
  return unwrapData(response, 'The deposit could not be paid.')
}

/**
 * Release held escrow to the guide/organizer after the trip has completed.
 * Only the tourist who made the deposit may call this.
 * Blocked when an open dispute exists on the associated trip.
 * Backend: POST /api/Payments/{paymentId}/release (Tourist only).
 */
export async function releaseEscrow(token, paymentId) {
  const response = await api.post(`/api/Payments/${paymentId}/release`, undefined, { token })
  return unwrapData(response, 'The escrow could not be released.')
}

/**
 * Release a single milestone of a milestone-allocated payment.
 * Only the tourist who made the deposit may call this, and only after the
 * associated trip has completed. Blocked when an open dispute exists.
 * Backend: POST /api/Payments/{paymentId}/milestones/{milestoneId}/release (Tourist only).
 */
export async function releaseMilestone(token, paymentId, milestoneId) {
  const response = await api.post(`/api/Payments/${paymentId}/milestones/${milestoneId}/release`, undefined, { token })
  return unwrapData(response, 'The milestone could not be released.')
}

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

// ── Cost breakdown ───────────────────────────────────────────────────────────

/**
 * Cost breakdown for a proposal. Currency comes from the response.
 * Backend: GET /api/cost-breakdown/proposal/{proposalId} (Tourist or Guide).
 */
export async function getProposalCostBreakdown(token, proposalId) {
  const response = await api.get(`/api/cost-breakdown/proposal/${proposalId}`, { token })
  return unwrapData(response, 'The cost breakdown could not be loaded.')
}

/**
 * Cost breakdown for a registration owned by the authenticated tourist.
 * Backend: GET /api/cost-breakdown/registration/{registrationId} (Tourist only).
 */
export async function getRegistrationCostBreakdown(token, registrationId) {
  const response = await api.get(`/api/cost-breakdown/registration/${registrationId}`, { token })
  return unwrapData(response, 'The cost breakdown could not be loaded.')
}

/**
 * Pre-registration cost estimate for an organized trip.
 * Backend: GET /api/cost-breakdown/organized-trip/{organizedTripId}/estimate?travelers=N.
 */
export async function getOrganizedTripCostEstimate(token, organizedTripId, travelers) {
  const count = Number.isInteger(travelers) && travelers > 0 ? travelers : 1
  const response = await api.get(
    `/api/cost-breakdown/organized-trip/${organizedTripId}/estimate?travelers=${count}`,
    { token },
  )

  return unwrapData(response, 'The cost estimate could not be loaded.')
}

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
