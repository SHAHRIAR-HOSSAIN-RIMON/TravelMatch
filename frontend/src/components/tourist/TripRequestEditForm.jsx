import { useMemo, useState } from 'react'
import { describeApiError, getFieldErrors } from '../../lib/api.js'
import {
  TRIP_TYPE_SUGGESTIONS,
  getMinStartDate,
  validateTripRequestDraft,
} from '../../lib/tripRequestValidation.js'
import { updateTripRequest } from '../../lib/touristService.js'
import FeedbackBanner from './FeedbackBanner.jsx'
import styles from './Tourist.module.css'

function numericOrNull(value) {
  if (value === null || value === undefined || value === '') return null
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

function sameNumber(left, right) {
  return numericOrNull(left) === numericOrNull(right)
}

function toInputValue(value) {
  return value === null || value === undefined ? '' : String(value)
}

function draftFromRequest(request) {
  return {
    destination: request.destination ?? '',
    tripType: request.tripType ?? '',
    startDate: request.startDate ?? '',
    endDate: request.endDate ?? '',
    numberOfTravelers: toInputValue(request.numberOfTravelers),
    budget: toInputValue(request.budget),
    budgetMin: toInputValue(request.budgetMin),
    budgetMax: toInputValue(request.budgetMax),
    description: request.description ?? '',
    travelPreferences: request.travelPreferences ?? '',
  }
}

/**
 * Only the fields the tourist actually changed are sent, matching the
 * "send what you want to change" contract of UpdateTripRequestDto.
 */
function buildUpdatePayload(request, draft) {
  const payload = {}

  const destination = draft.destination.trim()
  if (destination !== (request.destination ?? '').trim()) payload.destination = destination

  const tripType = draft.tripType.trim()
  if (tripType !== (request.tripType ?? '').trim()) payload.tripType = tripType

  if (draft.startDate && draft.startDate !== request.startDate) payload.startDate = draft.startDate
  if (draft.endDate && draft.endDate !== request.endDate) payload.endDate = draft.endDate

  if (!sameNumber(draft.numberOfTravelers, request.numberOfTravelers)) {
    payload.numberOfTravelers = Number.parseInt(draft.numberOfTravelers, 10)
  }

  if (!sameNumber(draft.budget, request.budget)) {
    payload.budget = Number.parseFloat(draft.budget)
  }

  if (!sameNumber(draft.budgetMin, request.budgetMin)) {
    const budgetMin = numericOrNull(draft.budgetMin)
    if (budgetMin !== null) payload.budgetMin = budgetMin
  }

  if (!sameNumber(draft.budgetMax, request.budgetMax)) {
    const budgetMax = numericOrNull(draft.budgetMax)
    if (budgetMax !== null) payload.budgetMax = budgetMax
  }

  const description = draft.description.trim()
  if (description !== (request.description ?? '').trim()) payload.description = description

  const travelPreferences = draft.travelPreferences.trim()
  if (travelPreferences !== (request.travelPreferences ?? '').trim()) {
    payload.travelPreferences = travelPreferences
  }

  return payload
}

function TripRequestEditForm({ request, token, onSaved, onCancelEdit }) {
  const original = useMemo(() => draftFromRequest(request), [request])
  const [draft, setDraft] = useState(original)
  const [errors, setErrors] = useState({})
  const [serverError, setServerError] = useState(null)
  const [isSaving, setIsSaving] = useState(false)
  const [showRange, setShowRange] = useState(Boolean(original.budgetMin || original.budgetMax))

  const payload = useMemo(() => buildUpdatePayload(request, draft), [draft, request])
  const hasChanges = Object.keys(payload).length > 0
  const minStartDate = getMinStartDate()

  function handleChange(field, value) {
    setDraft((current) => ({ ...current, [field]: value }))

    if (errors[field]) {
      setErrors((current) => {
        const next = { ...current }
        delete next[field]
        return next
      })
    }

    if (serverError) setServerError(null)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    if (isSaving) return

    setServerError(null)

    // The past-date rule is only re-checked when the start date was changed, so a
    // Pending request whose dates have already started can still have other fields edited.
    const nextErrors = validateTripRequestDraft(draft, {
      minStartDate: draft.startDate !== original.startDate ? minStartDate : '',
    })

    // The update endpoint treats a null field as "leave unchanged", so a cleared
    // range would silently keep the old values. Block it instead of pretending.
    if (request.budgetMin != null && numericOrNull(draft.budgetMin) === null) {
      nextErrors.budgetMin =
        'The existing minimum cannot be cleared. Enter a value or keep the current range.'
    }
    if (request.budgetMax != null && numericOrNull(draft.budgetMax) === null) {
      nextErrors.budgetMax =
        'The existing maximum cannot be cleared. Enter a value or keep the current range.'
    }

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors)
      const firstField = Object.keys(nextErrors)[0]
      document.getElementById(`edit-${firstField}`)?.focus()
      return
    }

    if (!hasChanges) {
      setServerError('Change at least one field before saving.')
      return
    }

    setIsSaving(true)

    try {
      const updated = await updateTripRequest(token, request.id, payload)
      onSaved(updated)
    } catch (error) {
      setErrors(getFieldErrors(error))
      setServerError(describeApiError(error, 'The trip request could not be updated.'))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <section className={styles.panel}>
      <h2 className={styles.sectionTitle}>Edit trip request</h2>
      <p className={styles.sectionLead}>
        The API only accepts changes while a request is Pending. Only the fields you change are
        sent.
      </p>

      <FeedbackBanner tone="error">{serverError}</FeedbackBanner>

      <form className={styles.form} noValidate onSubmit={handleSubmit}>
        <div className={styles.row}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-destination">
              Destination <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.destination ? 'edit-destination-error' : undefined}
              aria-invalid={errors.destination ? 'true' : undefined}
              className={`${styles.input} ${errors.destination ? styles.inputError : ''}`}
              id="edit-destination"
              onChange={(event) => handleChange('destination', event.target.value)}
              type="text"
              value={draft.destination}
            />
            {errors.destination ? (
              <p className={styles.errorText} id="edit-destination-error" role="alert">
                {errors.destination}
              </p>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-tripType">
              Trip type <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.tripType ? 'edit-tripType-error' : undefined}
              aria-invalid={errors.tripType ? 'true' : undefined}
              className={`${styles.input} ${errors.tripType ? styles.inputError : ''}`}
              id="edit-tripType"
              onChange={(event) => handleChange('tripType', event.target.value)}
              type="text"
              value={draft.tripType}
            />
            <div className={styles.chips}>
              {TRIP_TYPE_SUGGESTIONS.map((suggestion) => (
                <button
                  className={`${styles.chip} ${draft.tripType === suggestion ? styles.chipActive : ''}`}
                  key={suggestion}
                  onClick={() => handleChange('tripType', suggestion)}
                  type="button"
                >
                  {suggestion}
                </button>
              ))}
            </div>
            {errors.tripType ? (
              <p className={styles.errorText} id="edit-tripType-error" role="alert">
                {errors.tripType}
              </p>
            ) : null}
          </div>
        </div>

        <div className={styles.row}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-startDate">
              Start date <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.startDate ? 'edit-startDate-error' : undefined}
              aria-invalid={errors.startDate ? 'true' : undefined}
              className={`${styles.input} ${errors.startDate ? styles.inputError : ''}`}
              id="edit-startDate"
              min={minStartDate}
              onChange={(event) => handleChange('startDate', event.target.value)}
              type="date"
              value={draft.startDate}
            />
            {errors.startDate ? (
              <p className={styles.errorText} id="edit-startDate-error" role="alert">
                {errors.startDate}
              </p>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-endDate">
              End date <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.endDate ? 'edit-endDate-error' : undefined}
              aria-invalid={errors.endDate ? 'true' : undefined}
              className={`${styles.input} ${errors.endDate ? styles.inputError : ''}`}
              id="edit-endDate"
              min={draft.startDate || minStartDate}
              onChange={(event) => handleChange('endDate', event.target.value)}
              type="date"
              value={draft.endDate}
            />
            {errors.endDate ? (
              <p className={styles.errorText} id="edit-endDate-error" role="alert">
                {errors.endDate}
              </p>
            ) : null}
          </div>
        </div>

        <div className={styles.row}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-numberOfTravelers">
              Number of travellers <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.numberOfTravelers ? 'edit-numberOfTravelers-error' : undefined}
              aria-invalid={errors.numberOfTravelers ? 'true' : undefined}
              className={`${styles.input} ${errors.numberOfTravelers ? styles.inputError : ''}`}
              id="edit-numberOfTravelers"
              max="100"
              min="1"
              onChange={(event) => handleChange('numberOfTravelers', event.target.value)}
              step="1"
              type="number"
              value={draft.numberOfTravelers}
            />
            {errors.numberOfTravelers ? (
              <p className={styles.errorText} id="edit-numberOfTravelers-error" role="alert">
                {errors.numberOfTravelers}
              </p>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="edit-budget">
              Target budget <span className={styles.requiredMark}>*</span>
            </label>
            <input
              aria-describedby={errors.budget ? 'edit-budget-error' : undefined}
              aria-invalid={errors.budget ? 'true' : undefined}
              className={`${styles.input} ${errors.budget ? styles.inputError : ''}`}
              id="edit-budget"
              min="1"
              onChange={(event) => handleChange('budget', event.target.value)}
              step="any"
              type="number"
              value={draft.budget}
            />
            {errors.budget ? (
              <p className={styles.errorText} id="edit-budget-error" role="alert">
                {errors.budget}
              </p>
            ) : null}
          </div>
        </div>

        <div className={styles.field}>
          <button
            aria-expanded={showRange}
            className={styles.quietButton}
            onClick={() => setShowRange((current) => !current)}
            type="button"
          >
            {showRange ? '− Hide budget range' : '+ Add flexible budget range'}
          </button>

          {showRange ? (
            <div className={styles.row}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor="edit-budgetMin">
                  Minimum budget <span className={styles.optionalMark}>(optional)</span>
                </label>
                <input
                  aria-describedby={errors.budgetMin ? 'edit-budgetMin-error' : undefined}
                  aria-invalid={errors.budgetMin ? 'true' : undefined}
                  className={`${styles.input} ${errors.budgetMin ? styles.inputError : ''}`}
                  id="edit-budgetMin"
                  onChange={(event) => handleChange('budgetMin', event.target.value)}
                  step="any"
                  type="number"
                  value={draft.budgetMin}
                />
                {errors.budgetMin ? (
                  <p className={styles.errorText} id="edit-budgetMin-error" role="alert">
                    {errors.budgetMin}
                  </p>
                ) : null}
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="edit-budgetMax">
                  Maximum budget <span className={styles.optionalMark}>(optional)</span>
                </label>
                <input
                  aria-describedby={errors.budgetMax ? 'edit-budgetMax-error' : undefined}
                  aria-invalid={errors.budgetMax ? 'true' : undefined}
                  className={`${styles.input} ${errors.budgetMax ? styles.inputError : ''}`}
                  id="edit-budgetMax"
                  onChange={(event) => handleChange('budgetMax', event.target.value)}
                  type="number"
                  value={draft.budgetMax}
                />
                {errors.budgetMax ? (
                  <p className={styles.errorText} id="edit-budgetMax-error" role="alert">
                    {errors.budgetMax}
                  </p>
                ) : null}
              </div>
            </div>
          ) : null}
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="edit-description">
            Description <span className={styles.optionalMark}>(optional)</span>
          </label>
          <textarea
            aria-describedby={errors.description ? 'edit-description-error' : undefined}
            aria-invalid={errors.description ? 'true' : undefined}
            className={`${styles.textarea} ${errors.description ? styles.inputError : ''}`}
            id="edit-description"
            onChange={(event) => handleChange('description', event.target.value)}
            rows={4}
            value={draft.description}
          />
          <span className={styles.hintText}>{draft.description.length} / 2000 characters</span>
          {errors.description ? (
            <p className={styles.errorText} id="edit-description-error" role="alert">
              {errors.description}
            </p>
          ) : null}
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="edit-travelPreferences">
            Travel preferences <span className={styles.optionalMark}>(optional)</span>
          </label>
          <textarea
            aria-describedby={errors.travelPreferences ? 'edit-travelPreferences-error' : undefined}
            aria-invalid={errors.travelPreferences ? 'true' : undefined}
            className={`${styles.textarea} ${errors.travelPreferences ? styles.inputError : ''}`}
            id="edit-travelPreferences"
            onChange={(event) => handleChange('travelPreferences', event.target.value)}
            rows={3}
            value={draft.travelPreferences}
          />
          <span className={styles.hintText}>
            {draft.travelPreferences.length} / 1000 characters
          </span>
          {errors.travelPreferences ? (
            <p className={styles.errorText} id="edit-travelPreferences-error" role="alert">
              {errors.travelPreferences}
            </p>
          ) : null}
        </div>

        <div className={styles.formActions}>
          {hasChanges ? null : (
            <span className={styles.hintText}>No changes to save yet.</span>
          )}
          <button
            className={styles.secondaryButton}
            disabled={isSaving}
            onClick={onCancelEdit}
            type="button"
          >
            Discard changes
          </button>
          <button className={styles.primaryButton} disabled={isSaving || !hasChanges} type="submit">
            {isSaving ? (
              <>
                <span className={styles.spinner} aria-hidden="true" />
                Saving…
              </>
            ) : (
              'Save changes'
            )}
          </button>
        </div>
      </form>
    </section>
  )
}

export default TripRequestEditForm
