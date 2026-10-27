import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { raiseDispute } from '../../lib/disputeService.js'
import { describeApiError } from '../../lib/api.js'
import styles from './Disputes.module.css'

export function RaiseDisputeModal({
  isOpen,
  onClose,
  onSuccess,
  token,
  tripType = 'TripRequest',
  tripId,
  tripTitle,
}) {
  const [reason, setReason] = useState('')
  const [description, setDescription] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  useEffect(() => {
    if (isOpen) {
      setReason('')
      setDescription('')
      setError(null)
      setIsSubmitting(false)
    }
  }, [isOpen])

  if (!isOpen) return null

  async function handleSubmit(e) {
    e.preventDefault()
    if (!token) return

    const trimmedReason = reason.trim()
    const trimmedDescription = description.trim()

    if (!trimmedReason) {
      setError('A dispute reason or summary is required.')
      return
    }

    if (!trimmedDescription) {
      setError('A detailed description of the dispute is required.')
      return
    }

    setIsSubmitting(true)
    setError(null)

    try {
      const result = await raiseDispute(token, {
        tripType,
        tripId,
        reason: trimmedReason,
        description: trimmedDescription,
      })

      onSuccess?.(result)
      onClose()
    } catch (err) {
      setError(describeApiError(err, 'Could not submit dispute. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const tripLabel = tripType === 'OrganizedTrip' ? 'Organized Trip' : 'Trip Request'

  return createPortal(
    <div
      aria-modal="true"
      className={styles.modalOverlay}
      onClick={(e) => {
        if (e.target === e.currentTarget && !isSubmitting) onClose()
      }}
      role="dialog"
    >
      <div className={styles.modalContent}>
        <h2 className={styles.modalTitle}>Raise a Dispute</h2>
        <p className={styles.modalSubtitle}>
          Dispute for {tripLabel} #{tripId}
          {tripTitle ? ` (${tripTitle})` : ''}
        </p>

        <div className={styles.warningNotice}>
          <strong>Important:</strong> Raising a dispute will notify platform administrators and
          place an escrow hold on payments associated with this trip until the dispute is reviewed
          and resolved.
        </div>

        {error && <div className={styles.errorMessage}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="dispute-reason">
              Reason for Dispute <span className={styles.requiredMark}>*</span>
            </label>
            <input
              className={styles.input}
              id="dispute-reason"
              maxLength={200}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Guide did not attend the scheduled tour, service not rendered"
              required
              type="text"
              value={reason}
            />
          </div>

          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="dispute-description">
              Detailed Description <span className={styles.requiredMark}>*</span>
            </label>
            <textarea
              className={styles.textarea}
              id="dispute-description"
              maxLength={2000}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Please provide full details of what happened, timelines, agreements made, and what outcome you are requesting."
              required
              rows={5}
              value={description}
            />
            <span className={styles.charCount}>
              {description.length} / 2000 characters
            </span>
          </div>

          <div className={styles.modalActions}>
            <button
              className={styles.cancelButton}
              disabled={isSubmitting}
              onClick={onClose}
              type="button"
            >
              Cancel
            </button>
            <button
              className={styles.dangerSubmitButton}
              disabled={isSubmitting || !reason.trim() || !description.trim()}
              type="submit"
            >
              {isSubmitting ? 'Submitting…' : 'Submit Dispute'}
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}

export default RaiseDisputeModal

