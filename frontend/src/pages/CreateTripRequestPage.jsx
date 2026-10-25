import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import { formatDateRange, formatAmount } from '../components/dashboard/dashboardModel.js'
import { useAuth } from '../context/useAuth.js'
import { describeApiError, getFieldErrors } from '../lib/api.js'
import {
  TRIP_TYPE_SUGGESTIONS,
  getMinStartDate,
  validateTripRequestDraft,
} from '../lib/tripRequestValidation.js'
import { createTripRequest } from '../lib/touristService.js'
import styles from './CreateTripRequest.module.css'

function CreateTripRequestPage() {
  const { user, token, logout } = useAuth()
  const navigate = useNavigate()

  const [formData, setFormData] = useState({
    destination: '',
    tripType: '',
    startDate: '',
    endDate: '',
    numberOfTravelers: 1,
    budget: '',
    budgetMin: '',
    budgetMax: '',
    description: '',
    travelPreferences: '',
  })

  const [showRange, setShowRange] = useState(false)
  const [errors, setErrors] = useState({})
  const [serverError, setServerError] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [createdTrip, setCreatedTrip] = useState(null)

  const minStartDate = getMinStartDate()

  function handleChange(field, value) {
    setFormData((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev }
        delete next[field]
        return next
      })
    }
    if (serverError) {
      setServerError(null)
    }
  }

  function handleTripTypeSelect(type) {
    handleChange('tripType', type)
  }

  function validateForm() {
    return validateTripRequestDraft(formData, { minStartDate })
  }

  async function handleSubmit(e) {
    e.preventDefault()
    if (isSubmitting) return

    setServerError(null)
    const formErrors = validateForm()

    if (Object.keys(formErrors).length > 0) {
      setErrors(formErrors)
      const firstField = Object.keys(formErrors)[0]
      const element = document.getElementById(firstField)
      if (element) {
        element.focus()
      }
      return
    }

    setIsSubmitting(true)

    const payload = {
      destination: formData.destination.trim(),
      tripType: formData.tripType.trim(),
      startDate: formData.startDate,
      endDate: formData.endDate,
      numberOfTravelers: parseInt(formData.numberOfTravelers, 10),
      budget: parseFloat(formData.budget),
      budgetMin: formData.budgetMin ? parseFloat(formData.budgetMin) : null,
      budgetMax: formData.budgetMax ? parseFloat(formData.budgetMax) : null,
      description: formData.description.trim() || null,
      travelPreferences: formData.travelPreferences.trim() || null,
    }

    try {
      const created = await createTripRequest(token, payload)
      setCreatedTrip(created || payload)
    } catch (err) {
      setErrors(getFieldErrors(err))
      setServerError(describeApiError(err, 'Failed to create trip request.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  function handleReset() {
    setFormData({
      destination: '',
      tripType: '',
      startDate: '',
      endDate: '',
      numberOfTravelers: 1,
      budget: '',
      budgetMin: '',
      budgetMax: '',
      description: '',
      travelPreferences: '',
    })
    setErrors({})
    setServerError(null)
    setCreatedTrip(null)
    setShowRange(false)
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.topBar}>
          <Link className={styles.backLink} to="/dashboard">
            ← Back to Dashboard
          </Link>
        </div>

        {createdTrip ? (
          <section className={styles.successCard} aria-labelledby="success-heading">
            <div className={styles.successIconWrap} aria-hidden="true">
              ✓
            </div>
            <h1 className={styles.successTitle} id="success-heading">
              Trip Request Created!
            </h1>
            <p className={styles.successMessage}>
              Your trip request to <strong>{createdTrip.destination}</strong> has been successfully submitted.
              Local verified guides will be notified and can send you custom proposals.
            </p>

            <div className={styles.summaryBox}>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Destination</span>
                <span className={styles.summaryValue}>{createdTrip.destination}</span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Trip Type</span>
                <span className={styles.summaryValue}>{createdTrip.tripType}</span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Dates</span>
                <span className={styles.summaryValue}>
                  {formatDateRange(createdTrip.startDate, createdTrip.endDate)}
                </span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Travelers</span>
                <span className={styles.summaryValue}>
                  {createdTrip.numberOfTravelers}{' '}
                  {createdTrip.numberOfTravelers === 1 ? 'Traveler' : 'Travelers'}
                </span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Budget</span>
                <span className={styles.summaryValue}>
                  ৳{formatAmount(createdTrip.budget)} BDT
                </span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Status</span>
                <span className={styles.summaryValue}>Pending Proposals</span>
              </div>
            </div>

            <div className={styles.successActions}>
              <Link className={styles.dashboardBtn} to="/dashboard">
                View on Dashboard
              </Link>
              <button className={styles.anotherBtn} onClick={handleReset} type="button">
                Create Another Request
              </button>
            </div>
          </section>
        ) : (
          <div className={styles.card}>
            <header className={styles.header}>
              <span className={styles.badge}>Custom Itinerary</span>
              <h1 className={styles.title}>Create Trip Request</h1>
              <p className={styles.subtitle}>
                Tell local guides about your travel plan, destination, dates and budget to receive custom proposals.
              </p>
            </header>

            {serverError ? (
              <div className={styles.errorBanner} role="alert">
                <span className={styles.errorIcon} aria-hidden="true">
                  !
                </span>
                <span>{serverError}</span>
              </div>
            ) : null}

            <form className={styles.form} noValidate onSubmit={handleSubmit}>
              {/* Destination */}
              <div className={styles.field}>
                <label className={styles.label} htmlFor="destination">
                  Destination <span className={styles.requiredMark}>*</span>
                </label>
                <input
                  aria-describedby={errors.destination ? 'destination-error' : 'destination-helper'}
                  aria-invalid={errors.destination ? 'true' : undefined}
                  className={`${styles.input} ${errors.destination ? styles.inputError : ''}`}
                  id="destination"
                  name="destination"
                  onChange={(e) => handleChange('destination', e.target.value)}
                  placeholder="e.g., Cox's Bazar, Sylhet, Sajek Valley, Sreemangal"
                  type="text"
                  value={formData.destination}
                />
                <span className={styles.helperText} id="destination-helper">
                  City, region or tourist spot in Bangladesh (max 200 characters)
                </span>
                {errors.destination ? (
                  <p className={styles.fieldError} id="destination-error" role="alert">
                    {errors.destination}
                  </p>
                ) : null}
              </div>

              {/* Trip Type */}
              <div className={styles.field}>
                <label className={styles.label} htmlFor="tripType">
                  Trip Type <span className={styles.requiredMark}>*</span>
                </label>
                <input
                  aria-describedby={errors.tripType ? 'tripType-error' : 'tripType-helper'}
                  aria-invalid={errors.tripType ? 'true' : undefined}
                  className={`${styles.input} ${errors.tripType ? styles.inputError : ''}`}
                  id="tripType"
                  name="tripType"
                  onChange={(e) => handleChange('tripType', e.target.value)}
                  placeholder="e.g., Nature & Hiking, Beach, Cultural Tour"
                  type="text"
                  value={formData.tripType}
                />
                <div className={styles.pillsWrap} id="tripType-helper">
                  {TRIP_TYPE_SUGGESTIONS.map((suggestion) => (
                    <button
                      className={`${styles.pill} ${
                        formData.tripType === suggestion ? styles.pillActive : ''
                      }`}
                      key={suggestion}
                      onClick={() => handleTripTypeSelect(suggestion)}
                      type="button"
                    >
                      {suggestion}
                    </button>
                  ))}
                </div>
                {errors.tripType ? (
                  <p className={styles.fieldError} id="tripType-error" role="alert">
                    {errors.tripType}
                  </p>
                ) : null}
              </div>

              {/* Dates Row */}
              <div className={styles.row}>
                <div className={styles.field}>
                  <label className={styles.label} htmlFor="startDate">
                    Start Date <span className={styles.requiredMark}>*</span>
                  </label>
                  <input
                    aria-describedby={errors.startDate ? 'startDate-error' : undefined}
                    aria-invalid={errors.startDate ? 'true' : undefined}
                    className={`${styles.input} ${errors.startDate ? styles.inputError : ''}`}
                    id="startDate"
                    min={minStartDate}
                    name="startDate"
                    onChange={(e) => handleChange('startDate', e.target.value)}
                    type="date"
                    value={formData.startDate}
                  />
                  {errors.startDate ? (
                    <p className={styles.fieldError} id="startDate-error" role="alert">
                      {errors.startDate}
                    </p>
                  ) : null}
                </div>

                <div className={styles.field}>
                  <label className={styles.label} htmlFor="endDate">
                    End Date <span className={styles.requiredMark}>*</span>
                  </label>
                  <input
                    aria-describedby={errors.endDate ? 'endDate-error' : undefined}
                    aria-invalid={errors.endDate ? 'true' : undefined}
                    className={`${styles.input} ${errors.endDate ? styles.inputError : ''}`}
                    id="endDate"
                    min={formData.startDate || minStartDate}
                    name="endDate"
                    onChange={(e) => handleChange('endDate', e.target.value)}
                    type="date"
                    value={formData.endDate}
                  />
                  {errors.endDate ? (
                    <p className={styles.fieldError} id="endDate-error" role="alert">
                      {errors.endDate}
                    </p>
                  ) : null}
                </div>
              </div>

              {/* Travelers & Budget Row */}
              <div className={styles.row}>
                <div className={styles.field}>
                  <label className={styles.label} htmlFor="numberOfTravelers">
                    Number of Travelers <span className={styles.requiredMark}>*</span>
                  </label>
                  <input
                    aria-describedby={errors.numberOfTravelers ? 'numberOfTravelers-error' : undefined}
                    aria-invalid={errors.numberOfTravelers ? 'true' : undefined}
                    className={`${styles.input} ${errors.numberOfTravelers ? styles.inputError : ''}`}
                    id="numberOfTravelers"
                    max="100"
                    min="1"
                    name="numberOfTravelers"
                    onChange={(e) => handleChange('numberOfTravelers', e.target.value)}
                    step="1"
                    type="number"
                    value={formData.numberOfTravelers}
                  />
                  {errors.numberOfTravelers ? (
                    <p className={styles.fieldError} id="numberOfTravelers-error" role="alert">
                      {errors.numberOfTravelers}
                    </p>
                  ) : null}
                </div>

                <div className={styles.field}>
                  <label className={styles.label} htmlFor="budget">
                    Target Budget (BDT) <span className={styles.requiredMark}>*</span>
                  </label>
                  <input
                    aria-describedby={errors.budget ? 'budget-error' : undefined}
                    aria-invalid={errors.budget ? 'true' : undefined}
                    className={`${styles.input} ${errors.budget ? styles.inputError : ''}`}
                    id="budget"
                    min="1"
                    name="budget"
                    onChange={(e) => handleChange('budget', e.target.value)}
                    placeholder="e.g., 25000"
                    step="any"
                    type="number"
                    value={formData.budget}
                  />
                  {errors.budget ? (
                    <p className={styles.fieldError} id="budget-error" role="alert">
                      {errors.budget}
                    </p>
                  ) : null}
                </div>
              </div>

              {/* Budget Range (Optional) */}
              <div className={styles.field}>
                <button
                  aria-expanded={showRange}
                  className={styles.rangeToggleBtn}
                  onClick={() => setShowRange((prev) => !prev)}
                  type="button"
                >
                  {showRange ? '− Hide budget range' : '+ Add flexible budget range (optional)'}
                </button>

                {showRange ? (
                  <div className={styles.rangeBox}>
                    <div className={styles.row}>
                      <div className={styles.field}>
                        <label className={styles.label} htmlFor="budgetMin">
                          Minimum Budget (BDT) <span className={styles.optionalMark}>(Optional)</span>
                        </label>
                        <input
                          aria-describedby={errors.budgetMin ? 'budgetMin-error' : undefined}
                          aria-invalid={errors.budgetMin ? 'true' : undefined}
                          className={`${styles.input} ${errors.budgetMin ? styles.inputError : ''}`}
                          id="budgetMin"
                          min="1"
                          name="budgetMin"
                          onChange={(e) => handleChange('budgetMin', e.target.value)}
                          placeholder="e.g., 20000"
                          step="any"
                          type="number"
                          value={formData.budgetMin}
                        />
                        {errors.budgetMin ? (
                          <p className={styles.fieldError} id="budgetMin-error" role="alert">
                            {errors.budgetMin}
                          </p>
                        ) : null}
                      </div>

                      <div className={styles.field}>
                        <label className={styles.label} htmlFor="budgetMax">
                          Maximum Budget (BDT) <span className={styles.optionalMark}>(Optional)</span>
                        </label>
                        <input
                          aria-describedby={errors.budgetMax ? 'budgetMax-error' : undefined}
                          aria-invalid={errors.budgetMax ? 'true' : undefined}
                          className={`${styles.input} ${errors.budgetMax ? styles.inputError : ''}`}
                          id="budgetMax"
                          min="1"
                          name="budgetMax"
                          onChange={(e) => handleChange('budgetMax', e.target.value)}
                          placeholder="e.g., 30000"
                          step="any"
                          type="number"
                          value={formData.budgetMax}
                        />
                        {errors.budgetMax ? (
                          <p className={styles.fieldError} id="budgetMax-error" role="alert">
                            {errors.budgetMax}
                          </p>
                        ) : null}
                      </div>
                    </div>
                  </div>
                ) : null}
              </div>

              {/* Description */}
              <div className={styles.field}>
                <label className={styles.label} htmlFor="description">
                  Trip Description <span className={styles.optionalMark}>(Optional)</span>
                </label>
                <textarea
                  aria-describedby={errors.description ? 'description-error' : undefined}
                  aria-invalid={errors.description ? 'true' : undefined}
                  className={`${styles.textarea} ${errors.description ? styles.inputError : ''}`}
                  id="description"
                  maxLength={2000}
                  name="description"
                  onChange={(e) => handleChange('description', e.target.value)}
                  placeholder="Describe your ideal itinerary, specific attractions you want to visit, or activities you enjoy..."
                  rows={4}
                  value={formData.description}
                />
                <span className={styles.charCount}>
                  {formData.description.length} / 2000 characters
                </span>
                {errors.description ? (
                  <p className={styles.fieldError} id="description-error" role="alert">
                    {errors.description}
                  </p>
                ) : null}
              </div>

              {/* Travel Preferences */}
              <div className={styles.field}>
                <label className={styles.label} htmlFor="travelPreferences">
                  Travel Preferences <span className={styles.optionalMark}>(Optional)</span>
                </label>
                <textarea
                  aria-describedby={errors.travelPreferences ? 'travelPreferences-error' : undefined}
                  aria-invalid={errors.travelPreferences ? 'true' : undefined}
                  className={`${styles.textarea} ${errors.travelPreferences ? styles.inputError : ''}`}
                  id="travelPreferences"
                  maxLength={1000}
                  name="travelPreferences"
                  onChange={(e) => handleChange('travelPreferences', e.target.value)}
                  placeholder="e.g., Preferred travel pace, dietary restrictions, preferred guide language, accessibility needs..."
                  rows={3}
                  value={formData.travelPreferences}
                />
                <span className={styles.charCount}>
                  {formData.travelPreferences.length} / 1000 characters
                </span>
                {errors.travelPreferences ? (
                  <p className={styles.fieldError} id="travelPreferences-error" role="alert">
                    {errors.travelPreferences}
                  </p>
                ) : null}
              </div>

              {/* Actions */}
              <div className={styles.actions}>
                <button
                  className={styles.cancelBtn}
                  onClick={() => navigate('/dashboard')}
                  type="button"
                >
                  Cancel
                </button>
                <button
                  className={styles.submitBtn}
                  disabled={isSubmitting}
                  type="submit"
                >
                  {isSubmitting ? (
                    <>
                      <span className={styles.spinner} aria-hidden="true" />
                      <span>Submitting...</span>
                    </>
                  ) : (
                    <span>Submit Trip Request</span>
                  )}
                </button>
              </div>
            </form>
          </div>
        )}
      </div>
    </DashboardLayout>
  )
}

export default CreateTripRequestPage
