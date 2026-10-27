import { useState } from 'react'
import { createPortal } from 'react-dom'
import { resolveDispute } from '../../lib/disputeService.js'
import { describeApiError } from '../../lib/api.js'
import styles from './Disputes.module.css'

function ResolveDisputeForm({
  token,
  dispute,
  onSuccess,
  onClose,
}) {
  const [resolutionNote, setResolutionNote] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  async function handleSubmit(e) {
    e.preventDefault()
    if (!token) return

    const trimmed = resolutionNote.trim()
    if (!trimmed) {
      setError('A resolution note explaining the settlement is required.')
      return
    }

    if (trimmed.length > 2000) {
      setError('Resolution note must not exceed 2000 characters.')
      return
    }

    setIsSubmitting(true)
    setError(null)

    try {
      const result = await resolveDispute(token, dispute.id, trimmed)
      onSuccess?.(result)
      onClose()
    } catch (err) {
      setError(describeApiError(err, 'Could not resolve dispute. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div
      aria-modal="true"
      className={styles.modalOverlay}
      onClick={(e) => {
        if (e.target === e.currentTarget && !isSubmitting) onClose()
      }}
      role="dialog"
    >
      <div className={styles.modalContent}>
        <h2 className={styles.modalTitle}>Resolve Dispute #{dispute.id}</h2>
        <p className={styles.modalSubtitle}>
          Regarding {dispute.tripType} #{dispute.tripId} — &ldquo;{dispute.reason}&rdquo;
        </p>

        <div className={styles.warningNotice}>
          <strong>Administrative Action:</strong> Resolving this dispute will mark it as{' '}
          <strong>Resolved</strong>, release the escrow block on the associated trip&apos;s payments,
          and notify all participating parties of your resolution note.
        </div>

        {error && <div className={styles.errorMessage}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="resolution-note">
              Resolution Note & Settlement Account <span className={styles.requiredMark}>*</span>
            </label>
            <textarea
              className={styles.textarea}
              id="resolution-note"
              maxLength={2000}
              onChange={(e) => setResolutionNote(e.target.value)}
              placeholder="State the findings of the investigation and the settlement reached (e.g., both parties agreed to release escrow / partial refund agreed / complaint dismissed after evidence review)."
              required
              rows={5}
              value={resolutionNote}
            />
            <span className={styles.charCount}>
              {resolutionNote.length} / 2000 characters
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
              className={styles.primarySubmitButton}
              disabled={isSubmitting || !resolutionNote.trim()}
              type="submit"
            >
              {isSubmitting ? 'Resolving…' : 'Confirm Resolution'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function ResolveDisputeModal({
  isOpen,
  onClose,
  onSuccess,
  token,
  dispute,
}) {
  const [formKey, setFormKey] = useState(0)

  if (!isOpen || !dispute) return null

  return createPortal(
    <ResolveDisputeForm
      key={formKey}
      token={token}
      dispute={dispute}
      onSuccess={onSuccess}
      onClose={() => {
        onClose()
        setFormKey((k) => k + 1)
      }}
    />,
    document.body,
  )
}

export default ResolveDisputeModal