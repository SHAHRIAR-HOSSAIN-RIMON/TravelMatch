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

/**
 * Build URL search query string from non-empty parameters.
 */
function toQueryString(params = {}) {
  const searchParams = new URLSearchParams()

  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.append(key, String(value))
    }
  })

  const qs = searchParams.toString()
  return qs ? `?${qs}` : ''
}

// ── Browse Available Trip Requests ──────────────────────────────────────────

/**
 * Get open trip requests available for guides to bid on.
 * Backend: GET /api/trip-requests/open (Guide only).
 * Query params: destination, tripType, startDateFrom, startDateTo, minBudget, maxBudget, sort, page, pageSize.
 */
export async function getOpenTripRequests(token, query = {}) {
  const qs = toQueryString(query)
  const response = await api.get(`/api/trip-requests/open${qs}`, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load open trip requests.')
  }

  return {
    success: envelope.success,
    data: Array.isArray(envelope.data) ? envelope.data : [],
    message: envelope.message,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
    totalOpenCount: response.totalOpenCount ?? response.TotalOpenCount ?? 0,
    page: response.page ?? response.Page ?? 1,
    pageSize: response.pageSize ?? response.PageSize ?? 20,
    totalPages: response.totalPages ?? response.TotalPages ?? 0,
    hasFiltersApplied: response.hasFiltersApplied ?? response.HasFiltersApplied ?? false,
    guideVerificationStatus: response.guideVerificationStatus ?? response.GuideVerificationStatus ?? 'Pending',
    canSubmitProposal: response.canSubmitProposal ?? response.CanSubmitProposal ?? false,
  }
}

/**
 * Get details of a single open trip request.
 * Backend: GET /api/trip-requests/{id} (Guide only).
 */
export async function getOpenTripRequestDetail(token, tripRequestId) {
  const response = await api.get(`/api/trip-requests/${tripRequestId}`, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load trip request details.')
  }

  return {
    success: envelope.success,
    data: envelope.data,
    message: envelope.message,
    guideVerificationStatus: response.guideVerificationStatus ?? response.GuideVerificationStatus ?? 'Pending',
    canSubmitProposal: response.canSubmitProposal ?? response.CanSubmitProposal ?? false,
  }
}

// ── Proposal Actions ─────────────────────────────────────────────────────────

/**
 * Step 1 of proposal submission: Create Draft proposal.
 * Backend: POST /api/Itinerary/proposals?tripRequestId={tripRequestId} (Guide only).
 * Body: { proposedPrice, message }
 */
export async function createProposalDraft(token, tripRequestId, { proposedPrice, message }) {
  const response = await api.post(
    `/api/Itinerary/proposals?tripRequestId=${tripRequestId}`,
    {
      proposedPrice: Number(proposedPrice),
      message: message.trim(),
    },
    { token },
  )

  return unwrapData(response, 'Could not create proposal draft.')
}

/**
 * Step 2 of proposal submission: Submit a Draft proposal (Draft → Submitted).
 * Backend: POST /api/trip-requests/{tripRequestId}/proposals/{proposalId}/submit (Guide only).
 * Body: { availability, inclusions, exclusions, notes }
 */
export async function submitProposal(
  token,
  tripRequestId,
  proposalId,
  { availability, inclusions, exclusions, notes },
) {
  const payload = {
    availability: availability.trim(),
    inclusions: inclusions.trim(),
    exclusions: exclusions.trim(),
    notes: notes ? notes.trim() : null,
  }

  const response = await api.post(
    `/api/trip-requests/${tripRequestId}/proposals/${proposalId}/submit`,
    payload,
    { token },
  )

  return unwrapData(response, 'Could not submit proposal.')
}

/**
 * Complete proposal creation & submission in one seamless flow:
 * 1. Creates Draft proposal via /api/Itinerary/proposals
 * 2. Immediately transitions Draft to Submitted via /api/trip-requests/{tripRequestId}/proposals/{proposalId}/submit
 */
export async function createAndSubmitProposal(token, tripRequestId, formValues) {
  const draft = await createProposalDraft(token, tripRequestId, {
    proposedPrice: formValues.proposedPrice,
    message: formValues.message,
  })

  const proposalId = draft.id ?? draft.Id
  if (!proposalId) {
    throw new Error('Failed to retrieve proposal identifier after creation.')
  }

  const submitted = await submitProposal(token, tripRequestId, proposalId, {
    availability: formValues.availability,
    inclusions: formValues.inclusions,
    exclusions: formValues.exclusions,
    notes: formValues.notes,
  })

  return submitted
}

/**
 * Update an editable proposal (Draft or Submitted status).
 * Backend: PUT /api/trip-requests/{tripRequestId}/proposals/{proposalId} (Guide only).
 * Body: { proposedPrice?, message?, availability?, inclusions?, exclusions?, notes? }
 */
export async function updateProposal(token, tripRequestId, proposalId, updates) {
  const payload = {}

  if (updates.proposedPrice !== undefined && updates.proposedPrice !== null && updates.proposedPrice !== '') {
    payload.proposedPrice = Number(updates.proposedPrice)
  }
  if (typeof updates.message === 'string') {
    payload.message = updates.message.trim()
  }
  if (typeof updates.availability === 'string') {
    payload.availability = updates.availability.trim()
  }
  if (typeof updates.inclusions === 'string') {
    payload.inclusions = updates.inclusions.trim()
  }
  if (typeof updates.exclusions === 'string') {
    payload.exclusions = updates.exclusions.trim()
  }
  if (typeof updates.notes === 'string') {
    payload.notes = updates.notes.trim()
  }

  const response = await api.put(
    `/api/trip-requests/${tripRequestId}/proposals/${proposalId}`,
    payload,
    { token },
  )

  return unwrapData(response, 'Could not update proposal.')
}

/**
 * Withdraw a proposal (Draft or Submitted → Withdrawn).
 * Backend: POST /api/trip-requests/{tripRequestId}/proposals/{proposalId}/withdraw (Guide only).
 */
export async function withdrawProposal(token, tripRequestId, proposalId) {
  const response = await api.post(
    `/api/trip-requests/${tripRequestId}/proposals/${proposalId}/withdraw`,
    undefined,
    { token },
  )

  return unwrapData(response, 'Could not withdraw proposal.')
}

/**
 * List all proposals submitted by the authenticated guide.
 * Backend: GET /api/trip-requests/proposals/mine (Guide only).
 */
export async function getMyProposals(token) {
  return unwrapList(await api.get('/api/trip-requests/proposals/mine', { token }))
}

/**
 * Get proposal detail with full itinerary days (if any).
 * Backend: GET /api/Itinerary/proposals/{proposalId} (Guide only).
 */
export async function getProposalItinerary(token, proposalId) {
  const response = await api.get(`/api/Itinerary/proposals/${proposalId}`, { token })
  return unwrapData(response, 'Could not load proposal itinerary.')
}

// ── Financial and Activity Tracking for Accepted Trips ──────────────────────

/**
 * Itemized cost breakdown for a proposal.
 * Backend: GET /api/cost-breakdown/proposal/{proposalId} (Guide or Tourist authorized).
 */
export async function getProposalCostBreakdown(token, proposalId) {
  const response = await api.get(`/api/cost-breakdown/proposal/${proposalId}`, { token })
  return unwrapData(response, 'Could not load cost breakdown.')
}

/**
 * Itinerary day tracking and activity statuses for a proposal.
 * Backend: GET /api/itinerary-tracking/proposals/{proposalId} (Guide or Tourist).
 */
export async function getItineraryTracking(token, proposalId) {
  const response = await api.get(`/api/itinerary-tracking/proposals/${proposalId}`, { token })
  return unwrapList(response)
}

/**
 * Update the status of an itinerary activity ("UPCOMING", "IN_PROGRESS", "COMPLETED").
 * Backend: PUT /api/itinerary-tracking/activities/{activityId}/status (Guide only).
 */
export async function updateActivityStatus(token, activityId, status) {
  const response = await api.put(
    `/api/itinerary-tracking/activities/${activityId}/status`,
    { status },
    { token },
  )
  return unwrapData(response, 'Could not update activity status.')
}

