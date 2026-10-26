/**
 * Validation rules for Guide proposal submission and updates, mirroring
 * CreateProposalDto, SubmitProposalDto, and UpdateProposalDto in TravelMatch.API.
 */

export function validateProposalForm(values) {
  const errors = {}

  // Proposed price
  const price = Number(values.proposedPrice)
  if (values.proposedPrice === '' || values.proposedPrice === null || values.proposedPrice === undefined) {
    errors.proposedPrice = 'Proposed price is required.'
  } else if (Number.isNaN(price) || price <= 0) {
    errors.proposedPrice = 'Proposed price must be greater than zero.'
  } else if (price > 100_000_000) {
    errors.proposedPrice = 'Proposed price exceeds reasonable limit.'
  }

  // Pitch / Message
  const message = typeof values.message === 'string' ? values.message.trim() : ''
  if (!message) {
    errors.message = 'Please provide an introduction or pitch for the tourist.'
  } else if (message.length > 2000) {
    errors.message = 'Pitch must be 2000 characters or fewer.'
  }

  // Availability
  const availability = typeof values.availability === 'string' ? values.availability.trim() : ''
  if (!availability) {
    errors.availability = 'Availability details are required (e.g., specific dates/times available).'
  } else if (availability.length > 500) {
    errors.availability = 'Availability must be 500 characters or fewer.'
  }

  // Inclusions
  const inclusions = typeof values.inclusions === 'string' ? values.inclusions.trim() : ''
  if (!inclusions) {
    errors.inclusions = 'Please specify what is included in your proposal.'
  } else if (inclusions.length > 2000) {
    errors.inclusions = 'Inclusions must be 2000 characters or fewer.'
  }

  // Exclusions
  const exclusions = typeof values.exclusions === 'string' ? values.exclusions.trim() : ''
  if (!exclusions) {
    errors.exclusions = 'Please specify what is NOT included in your proposal.'
  } else if (exclusions.length > 2000) {
    errors.exclusions = 'Exclusions must be 2000 characters or fewer.'
  }

  // Notes
  if (values.notes && values.notes.length > 2000) {
    errors.notes = 'Additional notes must be 2000 characters or fewer.'
  }

  return errors
}

export function validateProposalUpdate(values) {
  const errors = {}

  if (values.proposedPrice !== undefined && values.proposedPrice !== '') {
    const price = Number(values.proposedPrice)
    if (Number.isNaN(price) || price <= 0) {
      errors.proposedPrice = 'Proposed price must be greater than zero.'
    }
  }

  if (values.message && values.message.length > 2000) {
    errors.message = 'Pitch must be 2000 characters or fewer.'
  }

  if (values.availability && values.availability.length > 500) {
    errors.availability = 'Availability must be 500 characters or fewer.'
  }

  if (values.inclusions && values.inclusions.length > 2000) {
    errors.inclusions = 'Inclusions must be 2000 characters or fewer.'
  }

  if (values.exclusions && values.exclusions.length > 2000) {
    errors.exclusions = 'Exclusions must be 2000 characters or fewer.'
  }

  if (values.notes && values.notes.length > 2000) {
    errors.notes = 'Additional notes must be 2000 characters or fewer.'
  }

  return errors
}

