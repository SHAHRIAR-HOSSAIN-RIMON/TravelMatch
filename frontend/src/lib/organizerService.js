/**
 * Organizer API service — Phase 4 (TRAV-22, TRAV-23, TRAV-28)
 *
 * All endpoints require the caller to hold an Organizer role JWT.
 * The backend controller is OrganizedTripsController at /api/OrganizedTrips.
 * Response envelopes follow the same { success, data, message } shape used
 * by every other service in this project.
 */

import { api } from './api.js'

// ── Envelope helpers (same pattern as touristService.js) ──────────────────────

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

function unwrapList(response) {
  const envelope = normalizeEnvelope(response)
  if (envelope.success === false) {
    throw new Error(envelope.message || 'The request could not be completed.')
  }
  return Array.isArray(envelope.data) ? envelope.data : []
}

// ── TRAV-23: Read organizer's own trips ───────────────────────────────────────

/**
 * All organized trips owned by the authenticated organizer, newest first.
 * Backend: GET /api/OrganizedTrips/my  [Authorize(Roles = "Organizer")]
 */
export async function getMyOrganizedTrips(token) {
  return unwrapList(await api.get('/api/OrganizedTrips/my', { token }))
}

// ── TRAV-22: Create ───────────────────────────────────────────────────────────

/**
 * Create a new organized trip as a Draft.
 * Backend: POST /api/OrganizedTrips  [Authorize(Roles = "Organizer")]
 *
 * payload: CreateOrganizedTripDto
 *   { title, destination, description, startDate, endDate,
 *     maxParticipants, pricePerPerson, inclusions, exclusions?,
 *     registrationDeadline }
 *
 * Returns OrganizedTripResponseDto on success.
 */
export async function createOrganizedTrip(token, payload) {
  const response = await api.post('/api/OrganizedTrips', payload, { token })
  return unwrapData(response, 'The organized trip could not be created.')
}

// ── TRAV-23: Update ───────────────────────────────────────────────────────────

/**
 * Replace the editable fields of a trip.
 * Backend: PUT /api/OrganizedTrips/{id}  [Authorize(Roles = "Organizer")]
 * Allowed statuses: Draft, ApplicationOpen, GuideSelection.
 * Returns OrganizedTripResponseDto on success.
 */
export async function updateOrganizedTrip(token, tripId, payload) {
  const response = await api.put(`/api/OrganizedTrips/${tripId}`, payload, { token })
  return unwrapData(response, 'The organized trip could not be updated.')
}

// ── TRAV-23: Publish ──────────────────────────────────────────────────────────

/**
 * Publish a Draft trip, moving it to ApplicationOpen.
 * Backend: POST /api/OrganizedTrips/{id}/publish  [Authorize(Roles = "Organizer")]
 * Returns OrganizedTripResponseDto on success.
 */
export async function publishOrganizedTrip(token, tripId) {
  const response = await api.post(`/api/OrganizedTrips/${tripId}/publish`, undefined, { token })
  return unwrapData(response, 'The trip could not be published.')
}

// ── TRAV-23: Cancel ───────────────────────────────────────────────────────────

/**
 * Cancel a trip.
 * Backend: POST /api/OrganizedTrips/{id}/cancel  [Authorize(Roles = "Organizer")]
 * Blocked for InProgress and Completed statuses.
 * Returns OrganizedTripResponseDto on success.
 */
export async function cancelOrganizedTrip(token, tripId) {
  const response = await api.post(`/api/OrganizedTrips/${tripId}/cancel`, undefined, { token })
  return unwrapData(response, 'The trip could not be cancelled.')
}

// ── TRAV-23: Complete ─────────────────────────────────────────────────────────

/**
 * Mark a trip as Completed.
 * Backend: POST /api/OrganizedTrips/{id}/complete  [Authorize(Roles = "Organizer")]
 * Requires end date to have passed; allowed from RegistrationOpen,
 * RegistrationClosed, InProgress.
 * Returns OrganizedTripResponseDto on success.
 */
export async function completeOrganizedTrip(token, tripId) {
  const response = await api.post(`/api/OrganizedTrips/${tripId}/complete`, undefined, { token })
  return unwrapData(response, 'The trip could not be marked as completed.')
}

// ── TRAV-28: View registrations ───────────────────────────────────────────────

/**
 * All registrations for one of the organizer's trips.
 * Backend: GET /api/OrganizedTrips/{id}/registrations  [Authorize(Roles = "Organizer")]
 * Returns RegistrationResponseDto[] — each item includes a Tourist sub-object
 * with { id, fullName, email, phoneNumber }.
 */
export async function getTripRegistrations(token, tripId) {
  return unwrapList(await api.get(`/api/OrganizedTrips/${tripId}/registrations`, { token }))
}

// ── TRAV-28: Cancel registration as organizer ─────────────────────────────────

/**
 * Cancel one tourist registration on the organizer's own trip.
 * Backend: POST /api/OrganizedTrips/{tripId}/registrations/{regId}/cancel
 *   [Authorize(Roles = "Organizer")]
 * Body: { cancellationReason: string | null }
 * Returns RegistrationResponseDto on success.
 */
export async function cancelRegistrationAsOrganizer(token, tripId, registrationId, cancellationReason) {
  const response = await api.post(
    `/api/OrganizedTrips/${tripId}/registrations/${registrationId}/cancel`,
    { cancellationReason: cancellationReason || null },
    { token },
  )
  return unwrapData(response, 'The registration could not be cancelled.')
}
