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
 * Submit a review for a completed trip.
 * Backend: POST /api/reviews (Tourist only).
 * Body: { guideId: number (GuideProfile.Id), tripRequestId: number, rating: number (1-5), comment?: string }
 */
export async function submitReview(token, { guideId, tripRequestId, rating, comment }) {
  const payload = {
    guideId: Number(guideId),
    tripRequestId: Number(tripRequestId),
    rating: Number(rating),
    comment: comment?.trim() ? comment.trim() : null,
  }

  const response = await api.post('/api/reviews', payload, { token })
  return unwrapData(response, 'The review could not be submitted.')
}

/**
 * Get a page of reviews for a guide profile, newest first.
 * Backend: GET /api/reviews/guide/{guideProfileId}?page=1&pageSize=20 (Authenticated).
 */
export async function getGuideReviews(token, guideProfileId, page = 1, pageSize = 20) {
  const response = await api.get(
    `/api/reviews/guide/${guideProfileId}?page=${page}&pageSize=${pageSize}`,
    { token },
  )

  const envelope = normalizeEnvelope(response)
  if (envelope.success === false) {
    throw new Error(envelope.message || 'Could not load guide reviews.')
  }

  return {
    success: envelope.success,
    data: Array.isArray(envelope.data) ? envelope.data : [],
    message: envelope.message,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
    page: response.page ?? response.Page ?? page,
    pageSize: response.pageSize ?? response.PageSize ?? pageSize,
    totalPages: response.totalPages ?? response.TotalPages ?? 0,
  }
}

/**
 * Get guide's average rating and review count.
 * Backend: GET /api/reviews/guide/{guideProfileId}/rating (Authenticated).
 * Response: { guideId, averageRating, reviewCount }
 */
export async function getGuideRating(token, guideProfileId) {
  const response = await api.get(`/api/reviews/guide/${guideProfileId}/rating`, { token })
  return {
    guideId: response?.guideId ?? response?.GuideId ?? guideProfileId,
    averageRating: response?.averageRating ?? response?.AverageRating ?? null,
    reviewCount: response?.reviewCount ?? response?.ReviewCount ?? 0,
  }
}

