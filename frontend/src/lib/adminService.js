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

function toQueryString(params = {}) {
  const searchParams = new URLSearchParams()

  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '' && value !== 'All') {
      searchParams.append(key, String(value))
    }
  })

  const qs = searchParams.toString()
  return qs ? `?${qs}` : ''
}

/**
 * Returns aggregate platform counts for the admin dashboard.
 * Backend: GET /api/admin/dashboard (Admin only).
 * Data: { totalUsers, totalTourists, totalGuides, totalOrganizers, totalTripRequests,
 *         totalOrganizedTrips, totalRegistrations, totalProposals, totalPayments,
 *         totalOpenDisputes, totalPendingGuideVerifications }
 */
export async function getDashboardSummary(token) {
  const response = await api.get('/api/admin/dashboard', { token })
  return unwrapData(response, 'Could not load admin dashboard summary.')
}

/**
 * Returns a paged user list, optionally filtered by role and active status.
 * Backend: GET /api/admin/users (Admin only).
 * Query params: role (Tourist, Guide, Organizer, Admin), isActive (boolean), page, pageSize.
 */
export async function getUsers(token, params = {}) {
  const qs = toQueryString(params)
  const response = await api.get(`/api/admin/users${qs}`, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load users.')
  }

  return {
    success: envelope.success,
    data: Array.isArray(envelope.data) ? envelope.data : [],
    message: envelope.message,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
    totalPages: response.totalPages ?? response.TotalPages ?? 0,
    page: response.page ?? response.Page ?? (params.page || 1),
    pageSize: response.pageSize ?? response.PageSize ?? (params.pageSize || 20),
  }
}

/**
 * Activates or deactivates a user.
 * Backend: PATCH /api/admin/users/{userId}/status (Admin only).
 * Body: { isActive: boolean }
 */
export async function updateUserStatus(token, userId, isActive) {
  const response = await api.patch(
    `/api/admin/users/${userId}/status`,
    { isActive: Boolean(isActive) },
    { token },
  )
  return unwrapData(response, 'Could not update user status.')
}

/**
 * Returns a paged list of guide profiles whose VerificationStatus is Pending.
 * Backend: GET /api/admin/guide-verification/pending (Admin only).
 * Query params: page, pageSize.
 */
export async function getPendingGuideVerifications(token, params = {}) {
  const qs = toQueryString(params)
  const response = await api.get(`/api/admin/guide-verification/pending${qs}`, { token })
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load pending guide verifications.')
  }

  return {
    success: envelope.success,
    data: Array.isArray(envelope.data) ? envelope.data : [],
    message: envelope.message,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
    totalPages: response.totalPages ?? response.TotalPages ?? 0,
    page: response.page ?? response.Page ?? (params.page || 1),
    pageSize: response.pageSize ?? response.PageSize ?? (params.pageSize || 20),
  }
}

/**
 * Sets a guide's VerificationStatus to Verified or Rejected.
 * Backend: PATCH /api/admin/guide-verification/{guideUserId} (Admin only).
 * Note: guideUserId is the User.Id (from GuideVerificationResponseDto.userId).
 * Body: { status: 'Verified' | 'Rejected', note?: string }
 */
export async function setGuideVerificationStatus(token, guideUserId, { status, note }) {
  const payload = {
    status,
    note: note?.trim() ? note.trim() : null,
  }

  const response = await api.patch(
    `/api/admin/guide-verification/${guideUserId}`,
    payload,
    { token },
  )
  return unwrapData(response, 'Could not update guide verification status.')
}

