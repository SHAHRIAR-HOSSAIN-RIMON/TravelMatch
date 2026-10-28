/**
 * Validation for the trip request draft, mirroring the rules enforced by
 * TravelMatch.API/Services/TripRequestService.cs and the DataAnnotations on
 * CreateTripRequestDto / UpdateTripRequestDto.
 *
 * The draft holds the raw string values bound to the form inputs; parsed numbers
 * are only produced when the payload is built.
 */

export const TRIP_TYPE_SUGGESTIONS = [
  'Nature & Hiking',
  'Beach & Coastal',
  'Cultural & Heritage',
  'Adventure & Trekking',
  'City Exploration',
  'Wildlife & Safari',
]

/**
 * Earliest selectable start date. The backend compares against "today" in Asia/Dhaka.
 */
export function getMinStartDate() {
  const now = new Date()
  // Bangladesh is UTC+6
  const utc = now.getTime() + now.getTimezoneOffset() * 60000
  const dhakaTime = new Date(utc + 6 * 3600000)
  return dhakaTime.toISOString().slice(0, 10)
}

function hasValue(value) {
  return value !== '' && value !== null && value !== undefined
}

/**
 * @returns {Record<string, string>} error messages keyed by field id (empty when valid)
 */
export function validateTripRequestDraft(draft, { minStartDate = getMinStartDate() } = {}) {
  const errors = {}
  const destination = typeof draft.destination === 'string' ? draft.destination.trim() : ''
  const tripType = typeof draft.tripType === 'string' ? draft.tripType.trim() : ''

  if (!destination) {
    errors.destination = 'Destination is required.'
  } else if (destination.length > 200) {
    errors.destination = 'Destination must be 200 characters or fewer.'
  }

  if (!tripType) {
    errors.tripType = 'Trip type is required.'
  } else if (tripType.length > 100) {
    errors.tripType = 'Trip type must be 100 characters or fewer.'
  }

  if (!draft.startDate) {
    errors.startDate = 'Start date is required.'
  } else if (draft.startDate < minStartDate) {
    errors.startDate = 'Start date cannot be in the past.'
  }

  if (!draft.endDate) {
    errors.endDate = 'End date is required.'
  } else if (draft.startDate && draft.endDate < draft.startDate) {
    errors.endDate = 'End date cannot be before start date.'
  }

  const travelers = Number(draft.numberOfTravelers)

  if (!hasValue(draft.numberOfTravelers) && draft.numberOfTravelers !== 0) {
    errors.numberOfTravelers = 'Number of travelers is required.'
  } else if (Number.isNaN(travelers) || !Number.isInteger(travelers)) {
    errors.numberOfTravelers = 'Number of travelers must be a whole number.'
  } else if (travelers < 1) {
    errors.numberOfTravelers = 'Number of travelers must be at least 1.'
  } else if (travelers > 100) {
    errors.numberOfTravelers = 'Number of travelers cannot exceed 100.'
  }

  const budgetValue = Number(draft.budget)

  if (!hasValue(draft.budget)) {
    errors.budget = 'Budget is required.'
  } else if (Number.isNaN(budgetValue) || budgetValue <= 0) {
    errors.budget = 'Budget must be greater than zero.'
  } else if (budgetValue > 100_000_000) {
    errors.budget = 'Budget exceeds maximum allowed amount.'
  }

  const hasMin = hasValue(draft.budgetMin)
  const hasMax = hasValue(draft.budgetMax)

  if (hasMin || hasMax) {
    if (!hasMin || !hasMax) {
      errors.budgetMin = 'Both minimum and maximum budget are required if specifying a range.'
      errors.budgetMax = 'Both minimum and maximum budget are required if specifying a range.'
    } else {
      const minValue = Number(draft.budgetMin)
      const maxValue = Number(draft.budgetMax)

      if (Number.isNaN(minValue) || minValue <= 0) {
        errors.budgetMin = 'Minimum budget must be greater than zero.'
      }

      if (Number.isNaN(maxValue) || maxValue <= 0) {
        errors.budgetMax = 'Maximum budget must be greater than zero.'
      }

      if (minValue > 0 && maxValue > 0 && minValue > maxValue) {
        errors.budgetMax = 'Maximum budget cannot be less than minimum budget.'
      }
    }
  }

  if (draft.description && draft.description.length > 2000) {
    errors.description = 'Description must be 2000 characters or fewer.'
  }

  if (draft.travelPreferences && draft.travelPreferences.length > 1000) {
    errors.travelPreferences = 'Travel preferences must be 1000 characters or fewer.'
  }

  return errors
}
