/**
 * Itinerary Tracking REST service — Phase 6
 *
 * Wraps ItineraryTrackingController.
 * Real-time updates are pushed by ItineraryTrackingHub.
 *
 * Backend routes:
 *   GET    /api/itinerary-tracking/proposals/{proposalId}
 *          → ItineraryTrackingListResultDto
 *            { data: ItineraryDayWithActivitiesDto[] }
 *
 *   PUT    /api/itinerary-tracking/activities/{activityId}/status  [Guide only]
 *          body: { status: string }  ("UPCOMING" | "IN_PROGRESS" | "COMPLETED")
 *          → ItineraryTrackingResultDto { data: ItineraryActivityDto }
 *          Hub broadcasts: ActivityStatusUpdated(ItineraryActivityDto)
 *
 *   POST   /api/itinerary-tracking/activities  [Guide only]
 *          body: CreateItineraryActivityDto
 *            { itineraryDayId, orderIndex?, title, description? }
 *          → ItineraryTrackingResultDto { data: ItineraryActivityDto }
 *          Hub broadcasts: ActivityCreated(ItineraryActivityDto)
 *
 *   DELETE /api/itinerary-tracking/activities/{activityId}  [Guide only]
 *          Hub broadcasts: ActivityDeleted({ activityId })
 */

import { api } from './api.js'

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
  if (env.success === false) throw new Error(env.message || 'Failed to load tracking data.')
  return Array.isArray(env.data) ? env.data : []
}

function unwrapData(response, fallback) {
  const env = normalizeEnvelope(response)
  if (env.success === false) throw new Error(env.message || fallback)
  return env.data ?? response
}

/**
 * Load all itinerary days with their tracking activities for a proposal.
 * Both Tourist and Guide can call this.
 *
 * @param {string} token
 * @param {number} proposalId
 * @returns {Promise<ItineraryDayWithActivitiesDto[]>}
 */
export async function getItineraryTracking(token, proposalId, { signal } = {}) {
  const response = await api.get(
    `/api/itinerary-tracking/proposals/${proposalId}`,
    { token, signal },
  )
  return unwrapList(response)
}

/**
 * Update the status of an activity.
 * Guide-only. Hub pushes ActivityStatusUpdated to all proposal participants.
 *
 * @param {string} token
 * @param {number} activityId
 * @param {string} status  "UPCOMING" | "IN_PROGRESS" | "COMPLETED"
 * @returns {Promise<ItineraryActivityDto>}
 */
export async function updateActivityStatus(token, activityId, status) {
  const response = await api.put(
    `/api/itinerary-tracking/activities/${activityId}/status`,
    { status },
    { token },
  )
  return unwrapData(response, 'Activity status could not be updated.')
}

/**
 * Create a new tracking activity on a given itinerary day.
 * Guide-only.
 *
 * @param {string} token
 * @param {{ itineraryDayId: number, title: string, description?: string, orderIndex?: number }} payload
 * @returns {Promise<ItineraryActivityDto>}
 */
export async function createActivity(token, payload) {
  const response = await api.post(
    '/api/itinerary-tracking/activities',
    payload,
    { token },
  )
  return unwrapData(response, 'Activity could not be created.')
}

/**
 * Delete a tracking activity.
 * Guide-only.
 *
 * @param {string} token
 * @param {number} activityId
 */
export async function deleteActivity(token, activityId) {
  const response = await api.delete(
    `/api/itinerary-tracking/activities/${activityId}`,
    { token },
  )
  // DELETE may return the plain result envelope or null (204)
  if (!response) return
  const env = normalizeEnvelope(response)
  if (env.success === false) throw new Error(env.message || 'Activity could not be deleted.')
}

/** Activity status values the backend accepts */
export const ACTIVITY_STATUSES = ['UPCOMING', 'IN_PROGRESS', 'COMPLETED']
