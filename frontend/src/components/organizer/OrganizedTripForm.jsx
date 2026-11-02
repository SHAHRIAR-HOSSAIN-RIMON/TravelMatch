/**
 * Reusable form for creating and editing an organized trip.
 *
 * Used by both CreateOrganizedTripPage (empty draft) and the inline edit
 * panel on OrganizerTripDetailPage (draft pre-populated from trip data).
 *
 * Props
 * ─────
 * draft        {object}   Controlled form state (from emptyTripDraft / draftFromTrip)
 * errors       {object}   Field-level error messages keyed by field name
 * serverError  {string|null}
 * isSaving     {boolean}
 * onChange     (field, value) => void
 * onSubmit     (event) => void
 * onCancel     () => void   — optional; hides the cancel button when omitted
 * submitLabel  {string}   Button label (default: "Save trip")
 * isEdit       {boolean}  Adjusts the start-date minimum (skip past check for
 *                         trips that already have a past-ish start date saved)
 */

import FeedbackBanner from '../tourist/FeedbackBanner.jsx'
import { getTodayDateString } from '../../lib/organizerModel.js'
import styles from '../tourist/Tourist.module.css'

function Field({ id, label, required, optional, error, hint, children }) {
  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={id}>
        {label}{' '}
        {required && <span className={styles.requiredMark}>*</span>}
        {optional && <span className={styles.optionalMark}>(optional)</span>}
      </label>
      {children}
      {hint && !error ? <span className={styles.hintText}>{hint}</span> : null}
      {error ? (
        <p className={styles.errorText} id={`${id}-error`} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  )
}

function OrganizedTripForm({
  draft,
  errors,
  serverError,
  isSaving,
  onChange,
  onSubmit,
  onCancel,
  submitLabel = 'Save trip',
  isEdit = false,
}) {
  const today = getTodayDateString()
  // When editing, don't enforce "start date must be in future" for the min
  // attribute — the saved date might predate today and we don't want to block
  // other edits. The server still validates on submit.
  const startDateMin = isEdit ? undefined : today

  function field(name) {
    return {
      id: `trip-${name}`,
      'aria-describedby': errors[name] ? `trip-${name}-error` : undefined,
      'aria-invalid': errors[name] ? 'true' : undefined,
      className: `${styles.input} ${errors[name] ? styles.inputError : ''}`,
      value: draft[name],
      onChange: (e) => onChange(name, e.target.value),
    }
  }

  function textareaField(name) {
    return {
      id: `trip-${name}`,
      'aria-describedby': errors[name] ? `trip-${name}-error` : undefined,
      'aria-invalid': errors[name] ? 'true' : undefined,
      className: `${styles.textarea} ${errors[name] ? styles.inputError : ''}`,
      value: draft[name],
      onChange: (e) => onChange(name, e.target.value),
    }
  }

  return (
    <form className={styles.form} noValidate onSubmit={onSubmit}>
      <FeedbackBanner tone="error">{serverError}</FeedbackBanner>

      {/* Row 1 — Title / Destination */}
      <div className={styles.row}>
        <Field id="trip-title" label="Title" required error={errors.title}>
          <input
            {...field('title')}
            maxLength={200}
            placeholder="e.g. Cappadocia Balloon & Cave Tour"
            type="text"
          />
        </Field>

        <Field id="trip-destination" label="Destination" required error={errors.destination}>
          <input
            {...field('destination')}
            maxLength={200}
            placeholder="e.g. Göreme, Turkey"
            type="text"
          />
        </Field>
      </div>

      {/* Row 2 — Start / End date */}
      <div className={styles.row}>
        <Field id="trip-startDate" label="Start date" required error={errors.startDate}>
          <input
            {...field('startDate')}
            className={`${styles.input} ${errors.startDate ? styles.inputError : ''}`}
            min={startDateMin}
            type="date"
          />
        </Field>

        <Field id="trip-endDate" label="End date" required error={errors.endDate}>
          <input
            {...field('endDate')}
            className={`${styles.input} ${errors.endDate ? styles.inputError : ''}`}
            min={draft.startDate || startDateMin}
            type="date"
          />
        </Field>
      </div>

      {/* Row 3 — Registration deadline / Max participants */}
      <div className={styles.row}>
        <Field
          id="trip-registrationDeadline"
          label="Registration deadline"
          required
          error={errors.registrationDeadline}
          hint="Must be before the start date."
        >
          <input
            {...field('registrationDeadline')}
            className={`${styles.input} ${errors.registrationDeadline ? styles.inputError : ''}`}
            min={isEdit ? undefined : today}
            max={draft.startDate || undefined}
            type="date"
          />
        </Field>

        <Field
          id="trip-maxParticipants"
          label="Max participants"
          required
          error={errors.maxParticipants}
        >
          <input
            {...field('maxParticipants')}
            min={1}
            step={1}
            type="number"
          />
        </Field>
      </div>

      {/* Row 4 — Price per person */}
      <div className={styles.row}>
        <Field
          id="trip-pricePerPerson"
          label="Price per person"
          required
          error={errors.pricePerPerson}
          hint="No currency is stored; the backend records a numeric value only."
        >
          <input
            {...field('pricePerPerson')}
            min={0.01}
            step="any"
            type="number"
          />
        </Field>
      </div>

      {/* Description */}
      <Field
        id="trip-description"
        label="Description"
        required
        error={errors.description}
        hint={`${draft.description.length} / 2000 characters`}
      >
        <textarea {...textareaField('description')} maxLength={2000} rows={4} />
      </Field>

      {/* Inclusions */}
      <Field
        id="trip-inclusions"
        label="Inclusions"
        required
        error={errors.inclusions}
        hint={`${draft.inclusions.length} / 2000 characters — what is covered in the price`}
      >
        <textarea {...textareaField('inclusions')} maxLength={2000} rows={3} />
      </Field>

      {/* Exclusions */}
      <Field
        id="trip-exclusions"
        label="Exclusions"
        optional
        error={errors.exclusions}
        hint={`${draft.exclusions.length} / 2000 characters — what tourists must arrange separately`}
      >
        <textarea {...textareaField('exclusions')} maxLength={2000} rows={3} />
      </Field>

      <div className={styles.formActions}>
        {onCancel ? (
          <button
            className={styles.secondaryButton}
            disabled={isSaving}
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
        ) : null}

        <button
          className={styles.primaryButton}
          disabled={isSaving}
          type="submit"
        >
          {isSaving ? (
            <>
              <span className={styles.spinner} aria-hidden="true" />
              Saving…
            </>
          ) : (
            submitLabel
          )}
        </button>
      </div>
    </form>
  )
}

export default OrganizedTripForm
