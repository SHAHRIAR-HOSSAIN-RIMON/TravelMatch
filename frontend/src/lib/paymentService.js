import { api, describeApiError as describeGenericApiError } from './api.js'
import {
  PAYMENT_STATUS,
  MILESTONE_STATUS,
  normalizePaymentStatus,
  normalizeMilestoneStatus,
  paymentStatusMeta,
  milestoneStatusLabel,
} from '../components/dashboard/dashboardModel.js'

/**
 * Re-export status enums and formatters for unified access
 */
export {
  PAYMENT_STATUS,
  MILESTONE_STATUS,
  normalizePaymentStatus,
  normalizeMilestoneStatus,
  paymentStatusMeta,
  milestoneStatusLabel,
}

// ── Target types ─────────────────────────────────────────────────────────────

export const PAYMENT_TARGET_TYPE = {
  Proposal: 0,
  Registration: 1,
}

export function normalizeTargetType(value) {
  if (typeof value === 'number') return value
  if (typeof value === 'string') {
    const lower = value.trim().toLowerCase()
    if (lower === 'proposal') return PAYMENT_TARGET_TYPE.Proposal
    if (lower === 'registration') return PAYMENT_TARGET_TYPE.Registration
    const num = Number(value)
    if (Number.isFinite(num)) return num
  }
  return -1
}

export function targetTypeLabel(type) {
  const norm = normalizeTargetType(type)
  switch (norm) {
    case PAYMENT_TARGET_TYPE.Proposal:
      return 'Guide Proposal'
    case PAYMENT_TARGET_TYPE.Registration:
      return 'Organized Trip Registration'
    default:
      return 'Payment'
  }
}

// ── Payment Error Codes & Descriptions ───────────────────────────────────────

export const PAYMENT_ERROR_MESSAGES = {
  // Enum names
  InvalidRequest: 'The payment request was invalid or missing required details.',
  Unauthorized: 'You are not authorized to perform this payment action.',
  TouristProfileNotFound: 'Tourist profile not found for authenticated user.',
  OrganizerProfileNotFound: 'Organizer profile not found.',
  ProposalNotFound: 'The specified guide proposal could not be found.',
  TripRequestNotFound: 'Associated trip request not found.',
  RegistrationNotFound: 'The specified organized trip registration was not found.',
  OrganizedTripNotFound: 'Associated organized trip was not found.',
  PaymentNotFound: 'Payment record not found.',
  InvalidProposalStatus: 'Payments can only be made for accepted proposals.',
  InvalidRegistrationStatus: 'Cannot pay for a cancelled or closed registration.',
  AlreadyPaid: 'Payment has already been deposited for this item.',
  PaymentFailed: 'The simulated payment was declined by the payment gateway.',
  TripCapacityExceeded: 'This trip is already at full capacity.',
  RegistrationDeadlinePassed: 'The registration deadline for this trip has passed.',
  TripNotAvailableForRegistration: 'This trip is not open for registration.',
  TripNotCompleted: 'Escrow can only be released after the associated trip has completed.',
  EscrowAlreadySettled: 'This escrow deposit has already been released or refunded.',
  DisputeBlocksRelease: 'Release is blocked by an open dispute on this trip. Resolve the dispute first.',
  RefundExceedsHeldAmount: 'Requested refund amount exceeds the remaining held escrow amount.',
  NotRefundable: 'This payment is not in a refundable state (must be held in escrow).',
  DisputeResolutionRequired: 'An open dispute on this trip must be resolved by an administrator before refunding.',
  InvalidMilestone: 'A milestone amount or index is invalid.',
  MilestoneAmountMismatch: 'Milestone amounts must sum up to the total price exactly.',
  MilestoneAlreadySettled: 'This milestone has already been released or refunded.',
  MilestoneNotFound: 'The milestone does not exist on this payment record.',
  ServerError: 'An unexpected internal server error occurred while processing the payment.',

  // Numeric codes matching PaymentError enum (0..27)
  1: 'The payment request was invalid or missing required details.',
  2: 'You are not authorized to perform this payment action.',
  3: 'Tourist profile not found for authenticated user.',
  4: 'Organizer profile not found.',
  5: 'The specified guide proposal could not be found.',
  6: 'Associated trip request not found.',
  7: 'The specified organized trip registration was not found.',
  8: 'Associated organized trip was not found.',
  9: 'Payment record not found.',
  10: 'Payments can only be made for accepted proposals.',
  11: 'Cannot pay for a cancelled or closed registration.',
  12: 'Payment has already been deposited for this item.',
  13: 'The simulated payment was declined by the payment gateway.',
  14: 'This trip is already at full capacity.',
  15: 'The registration deadline for this trip has passed.',
  16: 'This trip is not open for registration.',
  17: 'Escrow can only be released after the associated trip has completed.',
  18: 'This escrow deposit has already been released or refunded.',
  19: 'Release is blocked by an open dispute on this trip. Resolve the dispute first.',
  20: 'Requested refund amount exceeds the remaining held escrow amount.',
  21: 'This payment is not in a refundable state (must be held in escrow).',
  22: 'An open dispute on this trip must be resolved by an administrator before refunding.',
  23: 'A milestone amount or index is invalid.',
  24: 'Milestone amounts must sum up to the total price exactly.',
  25: 'This milestone has already been released or refunded.',
  26: 'The milestone does not exist on this payment record.',
  27: 'An unexpected internal server error occurred while processing the payment.',
}

/**
 * Translates payment API error response into a human-readable message,
 * inspecting result.error / result.Error (numeric or string enum) and HTTP status codes.
 */
export function describePaymentError(error, fallback = 'The payment operation could not be completed.') {
  const data = error?.data

  if (data && typeof data === 'object') {
    const errorCode = data.error ?? data.Error
    if (errorCode !== null && errorCode !== undefined && PAYMENT_ERROR_MESSAGES[errorCode]) {
      return PAYMENT_ERROR_MESSAGES[errorCode]
    }

    if (typeof data.message === 'string' && data.message.trim()) {
      return data.message.trim()
    }
    if (typeof data.Message === 'string' && data.Message.trim()) {
      return data.Message.trim()
    }
  }

  // Fallback by HTTP status
  if (error?.status === 422) {
    return 'Escrow can only be released after the associated trip has completed.'
  }
  if (error?.status === 409) {
    return 'Action blocked: This payment was already processed, or an open dispute is blocking release.'
  }
  if (error?.status === 402) {
    return 'Simulated payment declined by the gateway. Please retry.'
  }
  if (error?.status === 403) {
    return 'You are not authorized to perform this payment action.'
  }
  if (error?.status === 404) {
    return 'The requested payment record or target trip was not found.'
  }

  return describeGenericApiError(error, fallback)
}

// ── Envelope unwrappers ──────────────────────────────────────────────────────

function normalizeEnvelope(response) {
  if (!response || typeof response !== 'object') {
    return { success: false, data: null, message: '', error: null }
  }

  return {
    success: response.success ?? response.Success ?? true,
    data: response.data ?? response.Data ?? null,
    message: response.message ?? response.Message ?? '',
    error: response.error ?? response.Error ?? null,
    totalCount: response.totalCount ?? response.TotalCount ?? 0,
  }
}

function unwrapList(response) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || 'The payment records could not be loaded.')
  }

  const list = Array.isArray(envelope.data) ? envelope.data : []
  return list.map(normalizePayment)
}

function unwrapData(response, fallbackMessage) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || fallbackMessage)
  }

  const raw = envelope.data ?? response
  return normalizePayment(raw)
}

function unwrapRawData(response, fallbackMessage) {
  const envelope = normalizeEnvelope(response)

  if (envelope.success === false) {
    throw new Error(envelope.message || fallbackMessage)
  }

  return envelope.data ?? response
}

// ── Model Normalizers ────────────────────────────────────────────────────────

export function normalizeMilestone(raw) {
  if (!raw || typeof raw !== 'object') return null

  const id = raw.milestoneId ?? raw.MilestoneId ?? raw.id ?? raw.Id
  const status = normalizeMilestoneStatus(raw.status ?? raw.Status)

  return {
    id,
    milestoneId: id,
    index: raw.index ?? raw.Index ?? 0,
    label: raw.label ?? raw.Label ?? `Milestone ${raw.index ?? raw.Index ?? 1}`,
    amount: Number(raw.amount ?? raw.Amount ?? 0),
    status,
    statusLabel: milestoneStatusLabel(status),
    createdAt: raw.createdAt ?? raw.CreatedAt ?? null,
    heldAt: raw.heldAt ?? raw.HeldAt ?? null,
    releasedAt: raw.releasedAt ?? raw.ReleasedAt ?? null,
    refundedAt: raw.refundedAt ?? raw.RefundedAt ?? null,
  }
}

export function normalizePayment(raw) {
  if (!raw || typeof raw !== 'object') return null

  const id = raw.paymentId ?? raw.PaymentId ?? raw.id ?? raw.Id
  const targetType = normalizeTargetType(raw.targetType ?? raw.TargetType)
  const status = normalizePaymentStatus(raw.status ?? raw.Status)

  const rawMilestones = raw.milestones ?? raw.Milestones ?? []
  const milestones = Array.isArray(rawMilestones)
    ? rawMilestones.map(normalizeMilestone).filter(Boolean)
    : []

  return {
    id,
    paymentId: id,
    touristUserId: raw.touristUserId ?? raw.TouristUserId ?? null,
    targetType,
    targetTypeLabel: targetTypeLabel(targetType),
    proposalId: raw.proposalId ?? raw.ProposalId ?? null,
    registrationId: raw.registrationId ?? raw.RegistrationId ?? null,
    amount: Number(raw.amount ?? raw.Amount ?? 0),
    currency: (raw.currency ?? raw.Currency ?? 'USD').toUpperCase(),
    status,
    statusMeta: paymentStatusMeta({ status }),
    transactionReference: raw.transactionReference ?? raw.TransactionReference ?? '',
    failureReason: raw.failureReason ?? raw.FailureReason ?? null,
    createdAt: raw.createdAt ?? raw.CreatedAt ?? null,
    completedAt: raw.completedAt ?? raw.CompletedAt ?? null,
    escrowReleasedAt: raw.escrowReleasedAt ?? raw.EscrowReleasedAt ?? null,
    refundedAmount: raw.refundedAmount ?? raw.RefundedAmount != null ? Number(raw.refundedAmount ?? raw.RefundedAmount) : null,
    refundedAt: raw.refundedAt ?? raw.RefundedAt ?? null,
    refundReason: raw.refundReason ?? raw.RefundReason ?? null,
    milestones,
  }
}

// ── Eligibility checks ───────────────────────────────────────────────────────

/**
 * Returns true if the payment escrow is currently held (or partially refunded with remainder held).
 */
export function isEscrowHeld(payment) {
  if (!payment) return false
  const status = normalizePaymentStatus(payment.status)
  return status === PAYMENT_STATUS.Held || status === PAYMENT_STATUS.PartiallyRefunded
}

/**
 * Returns true if escrow has been released to the guide/organizer.
 */
export function isEscrowReleased(payment) {
  if (!payment) return false
  const status = normalizePaymentStatus(payment.status)
  return status === PAYMENT_STATUS.Released || status === PAYMENT_STATUS.Completed
}

/**
 * Checks whether escrow can be released:
 * - Payment must be in Held or PartiallyRefunded status
 * - Trip must be completed
 * - No open dispute may block release
 */
export function canReleaseEscrow(payment, isTripCompleted = false, hasOpenDispute = false) {
  if (!payment) return false
  if (!isEscrowHeld(payment)) return false
  if (!isTripCompleted) return false
  if (hasOpenDispute) return false
  return true
}

/**
 * Checks whether a specific milestone can be released:
 * - Milestone must be in Held status
 * - Payment must be Held or PartiallyRefunded
 * - Trip must be completed
 * - No open dispute
 */
export function canReleaseMilestone(milestone, payment, isTripCompleted = false, hasOpenDispute = false) {
  if (!milestone || !payment) return false
  const mStatus = normalizeMilestoneStatus(milestone.status)
  if (mStatus !== MILESTONE_STATUS.Held) return false
  return canReleaseEscrow(payment, isTripCompleted, hasOpenDispute)
}

// ── API Operations ───────────────────────────────────────────────────────────

/**
 * Returns all payment records owned by the authenticated tourist.
 * Backend: GET /api/Payments/my (Tourist only).
 */
export async function getMyPayments(token) {
  const response = await api.get('/api/Payments/my', { token })
  return unwrapList(response)
}

/**
 * Deposit payment for an accepted guide proposal.
 * Backend: POST /api/Payments/proposal/{proposalId} (Tourist only).
 * Optional payload: { paymentMethod?: string, milestones?: Array<{ index, label, amount }> }
 */
export async function payForProposal(token, proposalId, payload = {}) {
  const response = await api.post(`/api/Payments/proposal/${proposalId}`, payload, { token })
  return unwrapData(response, 'The deposit could not be paid.')
}

/**
 * Deposit payment for an organized trip registration.
 * Backend: POST /api/Payments/registration/{registrationId} (Tourist only).
 * Optional payload: { paymentMethod?: string, milestones?: Array<{ index, label, amount }> }
 */
export async function payForRegistration(token, registrationId, payload = {}) {
  const response = await api.post(`/api/Payments/registration/${registrationId}`, payload, { token })
  return unwrapData(response, 'The registration deposit could not be paid.')
}

/**
 * Release full held escrow to the guide/organizer after trip completion.
 * Backend: POST /api/Payments/{paymentId}/release (Tourist only).
 */
export async function releaseEscrow(token, paymentId) {
  const response = await api.post(`/api/Payments/${paymentId}/release`, undefined, { token })
  return unwrapData(response, 'The escrow could not be released.')
}

/**
 * Release a single milestone from a milestone-allocated payment.
 * Backend: POST /api/Payments/{paymentId}/milestones/{milestoneId}/release (Tourist only).
 */
export async function releaseMilestone(token, paymentId, milestoneId) {
  const response = await api.post(
    `/api/Payments/${paymentId}/milestones/${milestoneId}/release`,
    undefined,
    { token },
  )
  return unwrapData(response, 'The milestone could not be released.')
}

/**
 * Cost breakdown for an accepted guide proposal.
 * Backend: GET /api/cost-breakdown/proposal/{proposalId} (Tourist or Guide).
 */
export async function getProposalCostBreakdown(token, proposalId) {
  const response = await api.get(`/api/cost-breakdown/proposal/${proposalId}`, { token })
  return unwrapRawData(response, 'The cost breakdown could not be loaded.')
}

/**
 * Cost breakdown for a registration owned by the authenticated tourist.
 * Backend: GET /api/cost-breakdown/registration/{registrationId} (Tourist only).
 */
export async function getRegistrationCostBreakdown(token, registrationId) {
  const response = await api.get(`/api/cost-breakdown/registration/${registrationId}`, { token })
  return unwrapRawData(response, 'The cost breakdown could not be loaded.')
}

/**
 * Pre-registration cost estimate for an organized trip.
 * Backend: GET /api/cost-breakdown/organized-trip/{organizedTripId}/estimate?travelers=N.
 */
export async function estimateOrganizedTripCost(token, organizedTripId, travelers = 1) {
  const count = Number.isInteger(travelers) && travelers > 0 ? travelers : 1
  const response = await api.get(
    `/api/cost-breakdown/organized-trip/${organizedTripId}/estimate?travelers=${count}`,
    { token },
  )
  return unwrapRawData(response, 'The cost estimate could not be loaded.')
}

// ── Role-specific payment operations (Organizer & Admin) ─────────────────────

/**
 * View all payment records for registrations on a specific organized trip.
 * Backend: GET /api/Payments/trip/{organizedTripId} (Organizer only).
 */
export async function getTripPayments(token, organizedTripId) {
  const response = await api.get(`/api/Payments/trip/${organizedTripId}`, { token })
  return unwrapList(response)
}

/**
 * Returns all payment records for a specific user.
 * Backend: GET /api/Payments/user/{targetUserId} (Admin only).
 */
export async function getPaymentsByUser(token, targetUserId) {
  const response = await api.get(`/api/Payments/user/${targetUserId}`, { token })
  return unwrapList(response)
}

/**
 * Issues a full or partial refund on a held payment.
 * Backend: POST /api/Payments/{paymentId}/refund (Admin only).
 * Payload: { amount?: number, reason?: string }
 */
export async function refundPayment(token, paymentId, payload = {}) {
  const response = await api.post(`/api/Payments/${paymentId}/refund`, payload, { token })
  return unwrapData(response, 'The refund could not be issued.')
}
