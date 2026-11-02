/**
 * View-model helpers for the Phase 4 Organizer module.
 *
 * Status vocabularies and validation rules mirror the backend enums and
 * OrganizedTripService.ValidateCreateRequest exactly so client-side feedback
 * matches what the API would return.
 */

// ── Status action rules ──────────────────────────────────────────────────────

/**
 * Statuses from which the organizer may edit trip fields.
 * Source: OrganizedTripService.UpdateAsync
 */
const EDITABLE_STATUSES = new Set(['Draft', 'ApplicationOpen', 'GuideSelection'])

/**
 * The only status from which Publish is allowed.
 * Source: OrganizedTripService.PublishAsync
 */
const PUBLISHABLE_STATUSES = new Set(['Draft'])

/**
 * Statuses from which Cancel is blocked.
 * Source: OrganizedTripService.CancelAsync
 */
const NON_CANCELLABLE_STATUSES = new Set(['InProgress', 'Completed', 'Cancelled'])

/**
 * Statuses from which Complete is allowed (requires end date also passed).
 * Source: OrganizedTripService.CompleteAsync / CompletableStatuses
 */
const COMPLETABLE_STATUSES = new Set(['RegistrationOpen', 'RegistrationClosed', 'InProgress'])

export function canEditTrip(status) {
  return EDITABLE_STATUSES.has(status)
}

export function canPublishTrip(status) {
  return PUBLISHABLE_STATUSES.has(status)
}

export function canCancelTrip(status) {
  return !NON_CANCELLABLE_STATUSES.has(status)
}

/**
 * The backend also checks end date < today, but the frontend cannot reliably
 * replicate UTC "today". We surface a hint in the UI; the server is authoritative.
 */
export function canCompleteTrip(status) {
  return COMPLETABLE_STATUSES.has(status)
}

export function isTripTerminal(status) {
  return status === 'Completed' || status === 'Cancelled'
}

// ── Form validation ──────────────────────────────────────────────────────────

/**
 * Returns today's date as a YYYY-MM-DD string in local time.
 * Used as the `min` attribute on date inputs and for past-date validation.
 */
export function getTodayDateString() {
  const d = new Date()
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * Validates a CreateOrganizedTripDto draft object.
 * Mirrors OrganizedTripService.ValidateCreateRequest field by field.
 *
 * @param {object} draft - form state
 * @param {object} [opts]
 * @param {boolean} [opts.skipDateInPast] - skip the start-date-in-past check
 *   (used when editing a pre-existing trip whose start date is already saved)
 * @returns {object} - { fieldName: errorMessage } map; empty means valid
 */
export function validateOrganizedTripDraft(draft, opts = {}) {
  const errors = {}

  // ── Required text fields ──
  if (!draft.title?.trim()) {
    errors.title = 'Title is required.'
  }

  if (!draft.destination?.trim()) {
    errors.destination = 'Destination is required.'
  }

  if (!draft.description?.trim()) {
    errors.description = 'Description is required.'
  }

  if (!draft.inclusions?.trim()) {
    errors.inclusions = 'Inclusions are required.'
  }

  // ── Dates ──
  const today = getTodayDateString()

  if (!draft.startDate) {
    errors.startDate = 'Start date is required.'
  } else if (!opts.skipDateInPast && draft.startDate < today) {
    errors.startDate = 'Start date cannot be in the past.'
  }

  if (!draft.endDate) {
    errors.endDate = 'End date is required.'
  } else if (draft.startDate && draft.endDate <= draft.startDate) {
    errors.endDate = 'End date must be after start date.'
  }

  if (!draft.registrationDeadline) {
    errors.registrationDeadline = 'Registration deadline is required.'
  } else if (draft.startDate && draft.registrationDeadline >= draft.startDate) {
    errors.registrationDeadline = 'Registration deadline must be before start date.'
  }

  // ── Numeric fields ──
  const maxParticipants = Number(draft.maxParticipants)
  if (!draft.maxParticipants && draft.maxParticipants !== 0) {
    errors.maxParticipants = 'Maximum participants is required.'
  } else if (!Number.isInteger(maxParticipants) || maxParticipants < 1) {
    errors.maxParticipants = 'Maximum participants must be at least 1.'
  }

  const price = Number(draft.pricePerPerson)
  if (!draft.pricePerPerson && draft.pricePerPerson !== 0) {
    errors.pricePerPerson = 'Price per person is required.'
  } else if (!Number.isFinite(price) || price <= 0) {
    errors.pricePerPerson = 'Price per person must be greater than zero.'
  }

  return errors
}

/**
 * Build the request payload from the form draft.
 * Sends dates as YYYY-MM-DD strings (DateOnly on the backend).
 * Sends null for an empty exclusions field.
 */
export function buildTripPayload(draft) {
  return {
    title: draft.title.trim(),
    destination: draft.destination.trim(),
    description: draft.description.trim(),
    startDate: draft.startDate,
    endDate: draft.endDate,
    maxParticipants: Number.parseInt(draft.maxParticipants, 10),
    pricePerPerson: Number.parseFloat(draft.pricePerPerson),
    inclusions: draft.inclusions.trim(),
    exclusions: draft.exclusions?.trim() || null,
    registrationDeadline: draft.registrationDeadline,
  }
}

/**
 * Populate a form draft from an existing OrganizedTripResponseDto.
 * DateOnly values arrive as "YYYY-MM-DD" strings which are exactly what
 * <input type="date"> expects.
 */
export function draftFromTrip(trip) {
  return {
    title: trip.title ?? '',
    destination: trip.destination ?? '',
    description: trip.description ?? '',
    startDate: trip.startDate ?? '',
    endDate: trip.endDate ?? '',
    maxParticipants: trip.maxParticipants != null ? String(trip.maxParticipants) : '',
    pricePerPerson: trip.pricePerPerson != null ? String(trip.pricePerPerson) : '',
    inclusions: trip.inclusions ?? '',
    exclusions: trip.exclusions ?? '',
    registrationDeadline: trip.registrationDeadline ?? '',
  }
}

/** Empty draft for the create form. */
export function emptyTripDraft() {
  return {
    title: '',
    destination: '',
    description: '',
    startDate: '',
    endDate: '',
    maxParticipants: '',
    pricePerPerson: '',
    inclusions: '',
    exclusions: '',
    registrationDeadline: '',
  }
}

// ── Status display ────────────────────────────────────────────────────────────

/**
 * Human-readable label and colour tone for each OrganizedTripStatus value.
 * The organizer sees a richer set of statuses than the tourist-facing view.
 *
 * Tones: 'progress' (teal), 'attention' (amber), 'closed' (grey)
 */
const ORGANIZER_TRIP_STATUS_META = {
  Draft:              { label: 'Draft',                   tone: 'attention' },
  ApplicationOpen:    { label: 'Open for applications',   tone: 'progress'  },
  GuideSelection:     { label: 'Selecting guide',         tone: 'attention' },
  RegistrationOpen:   { label: 'Registration open',       tone: 'progress'  },
  RegistrationClosed: { label: 'Registration closed',     tone: 'closed'    },
  InProgress:         { label: 'In progress',             tone: 'progress'  },
  Completed:          { label: 'Completed',               tone: 'closed'    },
  Cancelled:          { label: 'Cancelled',               tone: 'closed'    },
}

function humanizeStatus(status) {
  if (!status) return 'Unknown'
  return String(status)
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/^./, (c) => c.toUpperCase())
}

export function getOrganizerTripStatusMeta(status) {
  return (
    ORGANIZER_TRIP_STATUS_META[status] ?? { label: humanizeStatus(status), tone: 'closed' }
  )
}

// ── Registration status (organizer perspective) ───────────────────────────────

const REGISTRATION_STATUS_META = {
  PendingPayment: { label: 'Deposit due',  tone: 'attention' },
  Confirmed:      { label: 'Confirmed',    tone: 'progress'  },
  Cancelled:      { label: 'Cancelled',    tone: 'closed'    },
}

export function getRegistrationStatusMeta(status) {
  return (
    REGISTRATION_STATUS_META[status] ?? { label: humanizeStatus(status), tone: 'closed' }
  )
}

/** Whether an organizer may cancel a given registration. */
export function canCancelRegistration(status) {
  return status === 'PendingPayment' || status === 'Confirmed'
}
