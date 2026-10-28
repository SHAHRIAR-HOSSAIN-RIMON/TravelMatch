import { createPortal } from 'react-dom'
import DisputeStatusPill from './DisputeStatusPill.jsx'
import styles from './Disputes.module.css'

function formatDate(isoString) {
  if (!isoString) return '—'
  try {
    const date = new Date(isoString)
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date)
  } catch {
    return String(isoString)
  }
}

export function DisputeDetailModal({
  isOpen,
  onClose,
  dispute,
  onResolveClick,
}) {
  if (!isOpen || !dispute) return null

  const isResolved = dispute.status === 'Resolved'
  const isOpenStatus = dispute.status === 'Open'

  return createPortal(
    <div
      aria-modal="true"
      className={styles.modalOverlay}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
      role="dialog"
    >
      <div className={styles.modalContent}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
          <h2 className={styles.modalTitle} style={{ margin: 0 }}>
            Dispute #{dispute.id}
          </h2>
          <DisputeStatusPill status={dispute.status} />
        </div>

        <div className={styles.detailGrid}>
          <div className={styles.detailItem}>
            <span className={styles.detailLabel}>Trip Association</span>
            <span className={styles.detailValue}>
              {dispute.tripType} #{dispute.tripId}
            </span>
          </div>

          <div className={styles.detailItem}>
            <span className={styles.detailLabel}>Reported By User</span>
            <span className={styles.detailValue}>User #{dispute.reportedByUserId}</span>
          </div>

          <div className={styles.detailItem}>
            <span className={styles.detailLabel}>Date Submitted</span>
            <span className={styles.detailValue}>{formatDate(dispute.createdAt)}</span>
          </div>

          {dispute.resolvedAt && (
            <div className={styles.detailItem}>
              <span className={styles.detailLabel}>Date Resolved</span>
              <span className={styles.detailValue}>{formatDate(dispute.resolvedAt)}</span>
            </div>
          )}
        </div>

        <div className={styles.formGroup}>
          <label className={styles.label}>Dispute Reason</label>
          <p className={styles.detailValue} style={{ fontSize: 16 }}>{dispute.reason}</p>
        </div>

        <div className={styles.formGroup}>
          <label className={styles.label}>Full Description</label>
          <div
            style={{
              padding: '12px 14px',
              background: 'var(--tm-slate-50)',
              borderRadius: 'var(--tm-radius-md)',
              border: '1px solid var(--tm-border-soft)',
              fontSize: 14,
              color: 'var(--tm-ink)',
              whiteSpace: 'pre-line',
              lineHeight: 1.6,
            }}
          >
            {dispute.description}
          </div>
        </div>

        {isResolved && dispute.resolutionNote && (
          <div className={styles.resolutionBox}>
            <h4 className={styles.resolutionTitle}>
              Official Resolution {dispute.resolvedByUserId ? `(by Admin #${dispute.resolvedByUserId})` : ''}
            </h4>
            <p className={styles.resolutionText}>{dispute.resolutionNote}</p>
          </div>
        )}

        <div className={styles.modalActions}>
          <button className={styles.cancelButton} onClick={onClose} type="button">
            Close
          </button>
          {isOpenStatus && onResolveClick && (
            <button
              className={styles.primarySubmitButton}
              onClick={() => {
                onClose()
                onResolveClick(dispute)
              }}
              type="button"
            >
              Resolve Dispute…
            </button>
          )}
        </div>
      </div>
    </div>,
    document.body,
  )
}

export default DisputeDetailModal

