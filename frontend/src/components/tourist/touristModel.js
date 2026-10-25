/**
 * View-model helpers for the Phase 2 Tourist pages.
 *
 * Status vocabularies mirror the backend enums exactly; the formatters for dates,
 * budgets and currency are reused from the dashboard model so both surfaces agree.
 */

import { humanizeStatus, isClosedStatus } from '../dashboard/dashboardModel.js'

/** TripRequestStatus values whose proposals can still be acted on. */
export const OPEN_TRIP_STATUSES = new Set(['Pending', 'ProposalsReceived'])

/** TripRequestStatus values where a guide is already involved. */
export const ACTIVE_TRIP_STATUSES = new Set(['Matched', 'PaymentPending', 'Planned', 'InProgress'])

export function tripStatusGroup(status) {
  if (OPEN_TRIP_STATUSES.has(status)) return 'open'
  if (ACTIVE_TRIP_STATUSES.has(status)) return 'active'
  if (isClosedStatus(status)) return 'closed'
  return 'other'
}

/**
 * The backend only accepts updates while the request is Pending
 * (TripRequestService.UpdateAsync); anything else answers 403.
 */
export function isTripEditable(status) {
  return status === 'Pending'
}

/**
 * Mirrors ProposalDetailDto.IsEligibleForAcceptance for list rows, which do not
 * carry the flag. The detail endpoint remains the authoritative source.
 */
export function canAcceptProposal(tripStatus, proposalStatus) {
  return OPEN_TRIP_STATUSES.has(tripStatus) && proposalStatus === 'Submitted'
}

const PROPOSAL_STATUS_META = {
  Submitted: { label: 'Awaiting your decision', tone: 'progress' },
  Accepted: { label: 'Accepted', tone: 'progress' },
  Rejected: { label: 'Not selected', tone: 'closed' },
  Withdrawn: { label: 'Withdrawn', tone: 'closed' },
  Draft: { label: 'Draft', tone: 'closed' },
}

const REGISTRATION_STATUS_META = {
  PendingPayment: { label: 'Deposit due', tone: 'attention' },
  Confirmed: { label: 'Confirmed', tone: 'progress' },
  Cancelled: { label: 'Cancelled', tone: 'closed' },
}

const ORGANIZED_TRIP_STATUS_META = {
  Draft: { label: 'Draft', tone: 'closed' },
  ApplicationOpen: { label: 'Applications open', tone: 'progress' },
  GuideSelection: { label: 'Selecting guide', tone: 'progress' },
  RegistrationOpen: { label: 'Open for registration', tone: 'progress' },
  RegistrationClosed: { label: 'Registration closed', tone: 'closed' },
  InProgress: { label: 'In progress', tone: 'progress' },
  Completed: { label: 'Completed', tone: 'closed' },
  Cancelled: { label: 'Cancelled', tone: 'closed' },
}

function readMeta(metaMap, status) {
  return metaMap[status] ?? { label: humanizeStatus(status), tone: 'closed' }
}

export const getProposalStatusMeta = (status) => readMeta(PROPOSAL_STATUS_META, status)
export const getRegistrationStatusMeta = (status) => readMeta(REGISTRATION_STATUS_META, status)
export const getOrganizedTripStatusMeta = (status) => readMeta(ORGANIZED_TRIP_STATUS_META, status)

/** PaymentTargetType.CostBreakdownType values (serialized as integers). */
export const BREAKDOWN_TYPE_LABELS = {
  0: 'Guide proposal',
  1: 'Organized trip',
}

/**
 * Every forward transition exposed by api/trip-requests/{id}/lifecycle.
 * `allowedTransitions` from the backend decides which ones are currently legal.
 */
export const LIFECYCLE_STEPS = [
  {
    key: 'proposals-received',
    label: 'Mark proposals received',
    hint: 'Signals that guide proposals are in. Needs at least one submitted proposal.',
  },
  {
    key: 'matched',
    label: 'Confirm chosen guide',
    hint: 'Locks in the guide you accepted.',
    needsProposal: true,
  },
  {
    key: 'payment-pending',
    label: 'Record agreed price',
    hint: 'Stores the price agreed with your guide.',
    needsPrice: true,
  },
  {
    key: 'planned',
    label: 'Mark itinerary planned',
    hint: 'Use once the itinerary is agreed.',
  },
  {
    key: 'in-progress',
    label: 'Start the trip',
    hint: 'Use on the day you set off.',
  },
  {
    key: 'completed',
    label: 'Complete the trip',
    hint: 'Completing the trip is what allows escrow release.',
  },
]

export const CANCEL_STEP = {
  key: 'cancelled',
  label: 'Cancel trip request',
  hint: 'Cancelling is final for this request; the backend will not reopen it.',
}

const TRANSITION_KEYS = {
  ProposalsReceived: 'proposals-received',
  Matched: 'matched',
  PaymentPending: 'payment-pending',
  Planned: 'planned',
  InProgress: 'in-progress',
  Completed: 'completed',
  Cancelled: 'cancelled',
}

/**
 * The API reports transitions with enum names ("PaymentPending"); the step
 * definitions use kebab-case keys. Normalize so both shapes compare equal.
 */
export function normalizeAllowedTransitions(allowedTransitions) {
  if (!Array.isArray(allowedTransitions)) return []

  return allowedTransitions
    .map((transition) => {
      if (typeof transition !== 'string') return null
      const trimmed = transition.trim()
      if (!trimmed) return null

      const knownKey = Object.values(TRANSITION_KEYS).find(
        (key) => key.toLowerCase() === trimmed.toLowerCase(),
      )
      if (knownKey) return knownKey

      return (
        TRANSITION_KEYS[trimmed] ??
        trimmed.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
      )
    })
    .filter(Boolean)
}

export function nextLifecycleStep(allowedTransitions) {
  const allowed = normalizeAllowedTransitions(allowedTransitions)
  return LIFECYCLE_STEPS.find((step) => allowed.includes(step.key)) ?? null
}

export function formatTravelers(count) {
  const value = Number(count)

  if (!Number.isFinite(value) || value <= 0) return 'Not provided'

  return `${value} traveller${value === 1 ? '' : 's'}`
}

const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/

/**
 * Formats a DateOnly string ("2026-12-01") without the timezone drift that
 * `new Date('2026-12-01')` introduces.
 */
export function formatDateLabel(value) {
  if (typeof value !== 'string' || !DATE_ONLY_PATTERN.test(value)) return null

  const date = new Date(`${value}T00:00:00`)
  if (Number.isNaN(date.getTime())) return null

  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(date)
}

export function formatSpots(remainingSpots) {
  const value = Number(remainingSpots)

  if (!Number.isFinite(value)) return 'Spots not reported'
  if (value <= 0) return 'No confirmed spots left'

  return `${value} spot${value === 1 ? '' : 's'} left`
}

/**
 * Proposal detail returns an itinerary array; guard against nulls from the API.
 */
export function normalizeItinerary(itinerary) {
  return Array.isArray(itinerary) ? itinerary : []
}
