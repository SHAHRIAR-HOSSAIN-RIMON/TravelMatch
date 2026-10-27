import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import StarRating from './StarRating.jsx'
import { submitReview } from '../../lib/reviewService.js'
import { describeApiError } from '../../lib/api.js'
import styles from './Reviews.module.css'

const RATING_LABELS = {
  1: 'Poor (1/5)',
  2: 'Fair (2/5)',
  3: 'Good (3/5)',
  4: 'Very Good (4/5)',
  5: 'Exceptional (5/5)',
}

export function ReviewModal({
  isOpen,
  onClose,
  onSuccess,
  token,
  guideId,
  guideName = 'Guide',
  tripRequestId,
  tripDestination,
}) {
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  useEffect(() => {
    if (isOpen) {
      setRating(5)
      setComment('')
      setError(null)
      setIsSubmitting(false)
    }
  }, [isOpen])

  if (!isOpen) return null

  async function handleSubmit(e) {
    e.preventDefault()
    if (!token) return

    if (!rating || rating < 1 || rating > 5) {
      setError('Please select a star rating between 1 and 5.')
      return
    }

    if (comment.length > 2000) {
      setError('Comment must not exceed 2000 characters.')
      return
    }

    setIsSubmitting(true)
    setError(null)

    try {
      const result = await submitReview(token, {
        guideId,
        tripRequestId,
        rating,
        comment,
      })

      onSuccess?.(result)
      onClose()
    } catch (err) {
      setError(describeApiError(err, 'Failed to submit review. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

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
        <h2 className={styles.modalTitle}>Rate & Review Your Trip</h2>
        <p className={styles.modalSubtitle}>
          Share your experience with <strong>{guideName}</strong>
          {tripDestination ? ` for your trip to ${tripDestination}` : ''}.
        </p>

        {error && <div className={styles.errorMessage}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label className={styles.label}>
              Rating <span className={styles.requiredMark}>*</span>
            </label>
            <div className={styles.ratingSelectWrap}>
              <StarRating
                interactive
                max={5}
                onChange={(val) => setRating(val)}
                size={28}
                value={rating}
              />
              <span className={styles.ratingHelpText}>
                {RATING_LABELS[rating] || ''}
              </span>
            </div>
          </div>

          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="review-comment">
              Feedback & Comments (Optional)
            </label>
            <textarea
              className={styles.textarea}
              id="review-comment"
              maxLength={2000}
              onChange={(e) => setComment(e.target.value)}
              placeholder="What did you enjoy about the tour? How was the guide's communication and local knowledge?"
              rows={4}
              value={comment}
            />
            <span className={styles.charCount}>
              {comment.length} / 2000 characters
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
              className={styles.submitButton}
              disabled={isSubmitting || !rating}
              type="submit"
            >
              {isSubmitting ? 'Submitting…' : 'Submit Review'}
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}

export default ReviewModal

