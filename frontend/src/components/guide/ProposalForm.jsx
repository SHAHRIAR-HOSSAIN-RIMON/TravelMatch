import { useState } from 'react'
import { validateProposalForm, validateProposalUpdate } from '../../lib/proposalValidation.js'
import styles from './Guide.module.css'

export function ProposalForm({
  tripRequest,
  initialValues = {},
  isEditing = false,
  isSubmitting = false,
  onSubmit,
  onCancel,
}) {
  const [values, setValues] = useState({
    proposedPrice: initialValues.proposedPrice ?? '',
    message: initialValues.message ?? '',
    availability: initialValues.availability ?? '',
    inclusions: initialValues.inclusions ?? '',
    exclusions: initialValues.exclusions ?? '',
    notes: initialValues.notes ?? '',
  })

  const [errors, setErrors] = useState({})

  function updateField(field, value) {
    setValues((prev) => ({ ...prev, [field]: value }))
    setErrors((prev) => {
      if (!prev[field]) return prev
      const next = { ...prev }
      delete next[field]
      return next
    })
  }

  function handleSubmit(event) {
    event.preventDefault()
    const validationErrors = isEditing
      ? validateProposalUpdate(values)
      : validateProposalForm(values)

    setErrors(validationErrors)

    if (Object.keys(validationErrors).length > 0) {
      const firstField = Object.keys(validationErrors)[0]
      document.getElementById(firstField)?.focus()
      return
    }

    onSubmit(values)
  }

  const budgetHint = tripRequest?.budgetMin && tripRequest?.budgetMax
    ? `Tourist budget range: $${tripRequest.budgetMin} – $${tripRequest.budgetMax}`
    : tripRequest?.budget
    ? `Tourist budget: $${tripRequest.budget}`
    : null

  return (
    <form className={styles.proposalForm} onSubmit={handleSubmit} noValidate>
      <div className={styles.formGroup}>
        <label className={styles.formLabel} htmlFor="proposedPrice">
          Proposed Price ($ USD) {!isEditing && <span className={styles.requiredMark}>*</span>}
        </label>
        {budgetHint ? <span className={styles.charCount}>{budgetHint}</span> : null}
        <input
          className={`${styles.formInput} ${errors.proposedPrice ? styles.formInputError : ''}`}
          id="proposedPrice"
          inputMode="decimal"
          min="0.01"
          onChange={(e) => updateField('proposedPrice', e.target.value)}
          placeholder="e.g. 450.00"
          step="0.01"
          type="number"
          value={values.proposedPrice}
        />
        {errors.proposedPrice ? (
          <p className={styles.fieldError} role="alert">
            {errors.proposedPrice}
          </p>
        ) : null}
      </div>

      <div className={styles.formGroup}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <label className={styles.formLabel} htmlFor="message">
            Proposal Pitch & Summary {!isEditing && <span className={styles.requiredMark}>*</span>}
          </label>
          <span className={styles.charCount}>{values.message.length} / 2000</span>
        </div>
        <textarea
          className={`${styles.formTextarea} ${errors.message ? styles.formInputError : ''}`}
          id="message"
          maxLength={2000}
          onChange={(e) => updateField('message', e.target.value)}
          placeholder="Introduce yourself, highlight your local expertise, and give an overview of the proposed experience..."
          rows={4}
          value={values.message}
        />
        {errors.message ? (
          <p className={styles.fieldError} role="alert">
            {errors.message}
          </p>
        ) : null}
      </div>

      <div className={styles.formGroup}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <label className={styles.formLabel} htmlFor="availability">
            Availability Details {!isEditing && <span className={styles.requiredMark}>*</span>}
          </label>
          <span className={styles.charCount}>{values.availability.length} / 500</span>
        </div>
        <input
          className={`${styles.formInput} ${errors.availability ? styles.formInputError : ''}`}
          id="availability"
          maxLength={500}
          onChange={(e) => updateField('availability', e.target.value)}
          placeholder="e.g. Full-time availability throughout the requested trip dates"
          type="text"
          value={values.availability}
        />
        {errors.availability ? (
          <p className={styles.fieldError} role="alert">
            {errors.availability}
          </p>
        ) : null}
      </div>

      <div className={styles.formGroup}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <label className={styles.formLabel} htmlFor="inclusions">
            Inclusions (What is included) {!isEditing && <span className={styles.requiredMark}>*</span>}
          </label>
          <span className={styles.charCount}>{values.inclusions.length} / 2000</span>
        </div>
        <textarea
          className={`${styles.formTextarea} ${errors.inclusions ? styles.formInputError : ''}`}
          id="inclusions"
          maxLength={2000}
          onChange={(e) => updateField('inclusions', e.target.value)}
          placeholder="e.g. Dedicated guiding, custom itinerary, local transportation between sites, monument admissions"
          rows={3}
          value={values.inclusions}
        />
        {errors.inclusions ? (
          <p className={styles.fieldError} role="alert">
            {errors.inclusions}
          </p>
        ) : null}
      </div>

      <div className={styles.formGroup}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <label className={styles.formLabel} htmlFor="exclusions">
            Exclusions (What is NOT included) {!isEditing && <span className={styles.requiredMark}>*</span>}
          </label>
          <span className={styles.charCount}>{values.exclusions.length} / 2000</span>
        </div>
        <textarea
          className={`${styles.formTextarea} ${errors.exclusions ? styles.formInputError : ''}`}
          id="exclusions"
          maxLength={2000}
          onChange={(e) => updateField('exclusions', e.target.value)}
          placeholder="e.g. Flight tickets, hotel accommodations, tourist meals, personal souvenirs"
          rows={3}
          value={values.exclusions}
        />
        {errors.exclusions ? (
          <p className={styles.fieldError} role="alert">
            {errors.exclusions}
          </p>
        ) : null}
      </div>

      <div className={styles.formGroup}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <label className={styles.formLabel} htmlFor="notes">
            Additional Notes (Optional)
          </label>
          <span className={styles.charCount}>{(values.notes || '').length} / 2000</span>
        </div>
        <textarea
          className={`${styles.formTextarea} ${errors.notes ? styles.formInputError : ''}`}
          id="notes"
          maxLength={2000}
          onChange={(e) => updateField('notes', e.target.value)}
          placeholder="Any special conditions, weather advisories, packing recommendations, or flexibility terms..."
          rows={2}
          value={values.notes}
        />
        {errors.notes ? (
          <p className={styles.fieldError} role="alert">
            {errors.notes}
          </p>
        ) : null}
      </div>

      <div style={{ display: 'flex', gap: '12px', marginTop: '12px' }}>
        <button
          className={styles.primaryButton}
          disabled={isSubmitting}
          type="submit"
        >
          {isSubmitting
            ? 'Submitting…'
            : isEditing
            ? 'Save Changes'
            : 'Submit Proposal'}
        </button>

        {onCancel ? (
          <button
            className={styles.secondaryButton}
            disabled={isSubmitting}
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
        ) : null}
      </div>
    </form>
  )
}

export default ProposalForm

