/**
 * Pure view-model helpers for the Tourist dashboard.
 * Status values mirror TravelMatch.API/Models/TripRequestStatus.cs exactly.
 */

const OPEN_STATUSES = new Set(['Pending', 'ProposalsReceived'])
const ACTIVE_STATUSES = new Set(['Matched', 'PaymentPending', 'Planned', 'InProgress'])
const CLOSED_STATUSES = new Set(['Completed', 'Cancelled'])

/** Proposal lookups are per trip request, so the dashboard checks a bounded slice. */
export const PROPOSAL_CANDIDATE_LIMIT = 12

const PAYMENT_STATUS_NAMES = [
  'Pending',
  'Held',
  'Released',
  'Failed',
  'Refunded',
  'PartiallyRefunded',
  'Completed',
]

export const PAYMENT_STATUS = {
  Pending: 0,
  Held: 1,
  Released: 2,
  Failed: 3,
  Refunded: 4,
  PartiallyRefunded: 5,
  Completed: 6,
}

const MILESTONE_STATUS_NAMES = ['Held', 'Released', 'Refunded']

export const MILESTONE_STATUS = {
  Held: 0,
  Released: 1,
  Refunded: 2,
}

const STATUS_META = {
  Pending: { label: 'Awaiting proposals', tone: 'attention' },
  ProposalsReceived: { label: 'Proposals received', tone: 'progress' },
  Matched: { label: 'Guide matched', tone: 'progress' },
  PaymentPending: { label: 'Deposit due', tone: 'attention' },
  Planned: { label: 'Itinerary planned', tone: 'progress' },
  InProgress: { label: 'Trip in progress', tone: 'progress' },
  Completed: { label: 'Completed', tone: 'closed' },
  Cancelled: { label: 'Cancelled', tone: 'closed' },
}

const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/

export function humanizeStatus(status) {
  if (!status) return 'Status unavailable'

  return String(status)
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/^./, (character) => character.toUpperCase())
}

export function getStatusMeta(status) {
  return STATUS_META[status] ?? { label: humanizeStatus(status), tone: 'closed' }
}

export function isClosedStatus(status) {
  return CLOSED_STATUSES.has(status)
}

export function parseDateOnly(value) {
  if (typeof value !== 'string' || !DATE_ONLY_PATTERN.test(value)) return null

  const date = new Date(`${value}T00:00:00`)
  return Number.isNaN(date.getTime()) ? null : date
}

export function formatShortDate(value) {
  const date = parseDateOnly(value)
  if (!date) return null

  return new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short' }).format(date)
}

export function formatLongDate(value) {
  const rawDate = typeof value === 'string' ? new Date(value) : null

  if (rawDate && !Number.isNaN(rawDate.getTime())) {
    return new Intl.DateTimeFormat('en-US', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    }).format(rawDate)
  }

  const dateOnly = parseDateOnly(value)
  if (!dateOnly) return null

  return new Intl.DateTimeFormat('en-US', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(dateOnly)
}

export function formatDateRange(startDate, endDate) {
  const start = formatShortDate(startDate)
  const end = formatShortDate(endDate)
  const endYear = parseDateOnly(endDate)?.getFullYear()
  const startYear = parseDateOnly(startDate)?.getFullYear()

  if (start && end) {
    if (startYear === endYear) {
      return `${start} to ${end}, ${endYear}`
    }
    return `${start}, ${startYear} to ${end}, ${endYear}`
  }

  if (start) return `From ${start}, ${startYear}`
  if (end) return `Until ${end}, ${endYear}`

  return 'Dates not set'
}

export function formatNights(startDate, endDate) {
  const start = parseDateOnly(startDate)
  const end = parseDateOnly(endDate)
  if (!start || !end) return null

  const nights = Math.round((end - start) / 86_400_000)
  if (!Number.isFinite(nights) || nights < 0) return null
  if (nights === 0) return 'Same-day trip'

  return `${nights} night${nights === 1 ? '' : 's'}`
}

export function formatAmount(value) {
  const amount = Number(value)
  if (!Number.isFinite(amount)) return null

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(amount)
}

export function formatCurrency(amount, currency) {
  const value = Number(amount)
  if (!Number.isFinite(value)) return null

  const code = typeof currency === 'string' && currency.length === 3 ? currency.toUpperCase() : 'USD'

  const fractionDigits = Number.isInteger(value) ? 0 : 2

  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: code,
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    }).format(value)
  } catch {
    return `${code} ${formatAmount(value)}`
  }
}

export function formatBudget(request) {
  const minimum = Number(request?.budgetMin)
  const maximum = Number(request?.budgetMax)
  const budget = Number(request?.budget)

  if (Number.isFinite(minimum) && Number.isFinite(maximum) && minimum > 0 && maximum > 0) {
    return `${formatAmount(minimum)} to ${formatAmount(maximum)}`
  }

  if (Number.isFinite(budget) && budget > 0) {
    return formatAmount(budget)
  }

  return 'Not set'
}

export function formatMatchingScore(score) {
  const value = Number(score)
  if (score === null || score === undefined || !Number.isFinite(value)) return null

  const percent = value <= 1 ? Math.round(value * 100) : Math.round(value)
  return `Match ${Math.min(Math.max(percent, 0), 100)}`
}

export function formatRating(rating) {
  const value = Number(rating)
  if (rating === null || rating === undefined || !Number.isFinite(value)) return null

  return `${value.toFixed(1)} rating`
}

export function getInitials(fullName) {
  const parts = String(fullName ?? '')
    .trim()
    .split(/\s+/)
    .filter(Boolean)

  if (parts.length === 0) return 'TM'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()

  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase()
}

export function sortNewestFirst(records) {
  return [...records].sort((left, right) => {
    const leftTime = Date.parse(left?.createdAt ?? '')
    const rightTime = Date.parse(right?.createdAt ?? '')

    if (Number.isFinite(leftTime) && Number.isFinite(rightTime) && leftTime !== rightTime) {
      return rightTime - leftTime
    }

    return Number(right?.id ?? 0) - Number(left?.id ?? 0)
  })
}

export function normalizePaymentStatus(value) {
  if (typeof value === 'number') return value

  if (typeof value === 'string') {
    const namedIndex = PAYMENT_STATUS_NAMES.findIndex(
      (name) => name.toLowerCase() === value.trim().toLowerCase(),
    )
    if (namedIndex >= 0) return namedIndex

    const numeric = Number(value)
    return Number.isFinite(numeric) ? numeric : -1
  }

  return -1
}

export function normalizeMilestoneStatus(value) {
  if (typeof value === 'number') return value

  if (typeof value === 'string') {
    const namedIndex = MILESTONE_STATUS_NAMES.findIndex(
      (name) => name.toLowerCase() === value.trim().toLowerCase(),
    )
    if (namedIndex >= 0) return namedIndex

    const numeric = Number(value)
    return Number.isFinite(numeric) ? numeric : -1
  }

  return -1
}

export function paymentStatusMeta(payment) {
  switch (normalizePaymentStatus(payment?.status ?? payment?.Status)) {
    case PAYMENT_STATUS.Held:
      return { label: 'Held in escrow', tone: 'progress' }
    case PAYMENT_STATUS.PartiallyRefunded:
      return { label: 'Partially refunded — remainder held', tone: 'attention' }
    case PAYMENT_STATUS.Released:
    case PAYMENT_STATUS.Completed:
      return { label: 'Released to guide', tone: 'closed' }
    case PAYMENT_STATUS.Refunded:
      return { label: 'Refunded', tone: 'closed' }
    case PAYMENT_STATUS.Failed:
      return { label: 'Payment failed', tone: 'attention' }
    case PAYMENT_STATUS.Pending:
      return { label: 'Payment pending', tone: 'attention' }
    default:
      return { label: 'Status unavailable', tone: 'attention' }
  }
}

export function milestoneStatusLabel(status) {
  switch (normalizeMilestoneStatus(status)) {
    case MILESTONE_STATUS.Released:
      return 'Released'
    case MILESTONE_STATUS.Refunded:
      return 'Refunded'
    case MILESTONE_STATUS.Held:
      return 'Held'
    default:
      return 'Unknown'
  }
}

export function summarizeRequests(requests) {
  const summary = {
    total: requests.length,
    open: 0,
    active: 0,
    closed: 0,
    other: 0,
    inProgress: 0,
    depositDue: 0,
    awaitingProposals: 0,
    planned: 0,
  }

  requests.forEach((request) => {
    if (OPEN_STATUSES.has(request.status)) {
      summary.open += 1
      if (request.status === 'Pending') summary.awaitingProposals += 1
      return
    }

    if (ACTIVE_STATUSES.has(request.status)) {
      summary.active += 1
      if (request.status === 'InProgress') summary.inProgress += 1
      if (request.status === 'PaymentPending') summary.depositDue += 1
      if (request.status === 'Planned') summary.planned += 1
      return
    }

    if (CLOSED_STATUSES.has(request.status)) {
      summary.closed += 1
      return
    }

    summary.other += 1
  })

  return summary
}

export function summarizeProposals(proposals, candidateIds) {
  let total = 0
  let checked = 0
  let failed = 0

  candidateIds.forEach((tripRequestId) => {
    const status = proposals.statusById[tripRequestId]

    if (status === 'ready') {
      checked += 1
      total += proposals.byRequestId[tripRequestId]?.length ?? 0
    } else if (status === 'error') {
      failed += 1
    }
  })

  return {
    total,
    checked,
    failed,
    candidateCount: candidateIds.length,
    pending: candidateIds.length - checked - failed,
  }
}

export function summarizeEscrow(payments) {
  const heldByCurrency = new Map()
  const releasedByCurrency = new Map()
  let heldCount = 0
  let releasedCount = 0
  let refundedCount = 0

  payments.forEach((payment) => {
    const status = normalizePaymentStatus(payment?.status)
    const currency = typeof payment?.currency === 'string' ? payment.currency.toUpperCase() : 'USD'
    const amount = Number(payment?.amount)
    const safeAmount = Number.isFinite(amount) ? amount : 0

    if (status === PAYMENT_STATUS.Held) {
      heldCount += 1
      heldByCurrency.set(currency, (heldByCurrency.get(currency) ?? 0) + safeAmount)
      return
    }

    if (status === PAYMENT_STATUS.Released) {
      releasedCount += 1
      releasedByCurrency.set(currency, (releasedByCurrency.get(currency) ?? 0) + safeAmount)
      return
    }

    if (status === PAYMENT_STATUS.Refunded || status === PAYMENT_STATUS.PartiallyRefunded) {
      refundedCount += 1
    }
  })

  return {
    totalRecords: payments.length,
    heldCount,
    releasedCount,
    refundedCount,
    heldByCurrency: [...heldByCurrency.entries()].map(([currency, amount]) => ({ currency, amount })),
    releasedByCurrency: [...releasedByCurrency.entries()].map(([currency, amount]) => ({
      currency,
      amount,
    })),
  }
}

export function buildWelcomeSentence({ status, total, open, active }) {
  if (status === 'loading') return 'Gathering your trip activity.'
  if (status === 'error') return "We couldn't load your trip requests just now."
  if (total === 0) return 'No trip requests yet.'

  const openLabel = `${open} open trip request${open === 1 ? '' : 's'}`
  const activeLabel = `${active} trip${active === 1 ? '' : 's'} past the proposal stage`

  if (open > 0 && active > 0) return `${openLabel} and ${activeLabel}.`
  if (active > 0) return `${activeLabel[0].toUpperCase()}${activeLabel.slice(1)}.`
  if (open > 0) return `${openLabel} waiting on guide proposals.`

  return 'No open requests right now.'
}

function buildRequestsQualifier(summary) {
  if (summary.total === 0) return 'No trip requests yet.'
  if (summary.active === 0) return 'Nothing has matched with a guide yet.'
  if (summary.inProgress > 0) {
    return `${summary.inProgress} currently travelling with a guide.`
  }
  if (summary.depositDue > 0) {
    return `${summary.depositDue} waiting on your escrow deposit.`
  }
  return `${summary.active} matched or planned, none started yet.`
}

function buildOpenQualifier(summary) {
  if (summary.total === 0) return 'No trip requests yet.'
  if (summary.open === 0) return 'Nothing is waiting on guide proposals.'
  if (summary.awaitingProposals > 0) {
    return `${summary.awaitingProposals} still waiting for a first proposal.`
  }
  return 'Every open request has at least one proposal.'
}

function buildProposalsQualifier(proposalsSummary, isTruncated) {
  if (proposalsSummary.candidateCount === 0) return 'No open requests to collect proposals for.'

  if (proposalsSummary.failed > 0) {
    return `${proposalsSummary.checked} of ${proposalsSummary.candidateCount} requests checked. Some proposals could not load.`
  }

  if (isTruncated) {
    return `Only the latest ${proposalsSummary.candidateCount} open requests were checked.`
  }

  if (proposalsSummary.total === 0) {
    return `No submitted proposals from ${proposalsSummary.checked} checked request${proposalsSummary.checked === 1 ? '' : 's'}.`
  }

  return `Across ${proposalsSummary.checked} trip request${proposalsSummary.checked === 1 ? '' : 's'}.`
}

function buildEscrowItem(paymentsState, escrowSummary) {
  if (paymentsState.status === 'loading') {
    return { state: 'loading', value: null, qualifier: '' }
  }

  if (paymentsState.status === 'error') {
    return {
      state: 'error',
      value: null,
      qualifier: 'Payment records could not be loaded.',
      retryResource: 'payments',
      retryLabel: 'payments',
    }
  }

  if (escrowSummary.heldCount === 0) {
    const releasedNote =
      escrowSummary.releasedCount > 0
        ? ` ${escrowSummary.releasedCount} deposit${escrowSummary.releasedCount === 1 ? '' : 's'} released to guides.`
        : ''

    return {
      state: 'ready',
      emphasis: 'text',
      value: 'No funds held',
      qualifier: `Deposits you pay appear here as escrow.${releasedNote}`,
    }
  }

  if (escrowSummary.heldByCurrency.length === 1) {
    const [held] = escrowSummary.heldByCurrency
    const releasedNote =
      escrowSummary.releasedCount > 0
        ? `, ${escrowSummary.releasedCount} already released`
        : ''

    return {
      state: 'ready',
      emphasis: 'number',
      value: formatCurrency(held.amount, held.currency),
      qualifier: `${escrowSummary.heldCount} deposit${escrowSummary.heldCount === 1 ? '' : 's'} in escrow${releasedNote}.`,
    }
  }

  return {
    state: 'ready',
    emphasis: 'text',
    value: `${escrowSummary.heldCount} deposits`,
    qualifier: `Held in ${escrowSummary.heldByCurrency.length} currencies; totals are not combined.`,
  }
}

export function mergeProposalResults(current, tripRequestIds, results) {
  const byRequestId = { ...current.byRequestId }
  const statusById = { ...current.statusById }

  results.forEach((result, index) => {
    const tripRequestId = tripRequestIds[index]

    if (result.status === 'fulfilled') {
      byRequestId[tripRequestId] = result.value
      statusById[tripRequestId] = 'ready'
      return
    }

    byRequestId[tripRequestId] = []
    statusById[tripRequestId] = 'error'
  })

  return { byRequestId, statusById }
}

export function buildSummaryItems({ requestsState, paymentsState, requestsSummary, proposalsSummary, escrowSummary }) {
  const requestsLoading = requestsState.status === 'loading'
  const requestsFailed = requestsState.status === 'error'

  const requestsErrorItem = {
    state: 'error',
    value: null,
    qualifier: 'Needs trip request data.',
  }

  const activeItem = requestsLoading
    ? { state: 'loading', value: null, qualifier: '' }
    : requestsFailed
      ? requestsErrorItem
      : {
          state: 'ready',
          emphasis: 'number',
          value: String(requestsSummary.active),
          qualifier: buildRequestsQualifier(requestsSummary),
        }

  const openItem = requestsLoading
    ? { state: 'loading', value: null, qualifier: '' }
    : requestsFailed
      ? requestsErrorItem
      : {
          state: 'ready',
          emphasis: 'number',
          value: String(requestsSummary.open),
          qualifier: buildOpenQualifier(requestsSummary),
        }

  let proposalsItem

  if (requestsLoading) {
    proposalsItem = { state: 'loading', value: null, qualifier: '' }
  } else if (requestsFailed) {
    proposalsItem = requestsErrorItem
  } else if (proposalsSummary.pending > 0) {
    proposalsItem = { state: 'loading', value: null, qualifier: '' }
  } else if (proposalsSummary.failed > 0 && proposalsSummary.checked === 0) {
    proposalsItem = {
      state: 'error',
      value: null,
      qualifier: 'Proposals could not be loaded.',
      retryResource: 'proposals',
      retryLabel: 'proposals',
    }
  } else {
    proposalsItem = {
      state: 'ready',
      emphasis: 'number',
      value: String(proposalsSummary.total),
      qualifier: buildProposalsQualifier(
        proposalsSummary,
        requestsSummary.open + requestsSummary.active > proposalsSummary.candidateCount,
      ),
    }
  }

  return [
    { id: 'active-trips', label: 'Active trips', ...activeItem },
    { id: 'open-requests', label: 'Open trip requests', ...openItem },
    { id: 'proposals-received', label: 'Proposals received', ...proposalsItem },
    { id: 'escrow-status', label: 'Escrow status', ...buildEscrowItem(paymentsState, escrowSummary) },
  ]
}
