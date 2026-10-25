import { useEffect, useId, useRef } from 'react'
import { createPortal } from 'react-dom'
import styles from './Tourist.module.css'

/**
 * Confirmation step for destructive or irreversible tourist actions
 * (cancel request, accept proposal, pay a deposit, cancel registration).
 *
 * Escape and the backdrop close it, focus is trapped inside the panel and
 * returned to the trigger on close.
 */
function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  tone = 'primary',
  busy = false,
  onConfirm,
  onCancel,
  children,
}) {
  const panelRef = useRef(null)
  const cancelRef = useRef(null)
  const onCancelRef = useRef(onCancel)
  const busyRef = useRef(busy)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    onCancelRef.current = onCancel
  }, [onCancel])

  useEffect(() => {
    busyRef.current = busy
  }, [busy])

  useEffect(() => {
    if (!open) return undefined

    const previouslyFocused = document.activeElement
    const previousOverflow = document.body.style.overflow

    document.body.style.overflow = 'hidden'
    cancelRef.current?.focus()

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        if (busyRef.current) return
        event.preventDefault()
        onCancelRef.current?.()
        return
      }

      if (event.key !== 'Tab') return

      const panel = panelRef.current
      if (!panel) return

      const focusable = Array.from(
        panel.querySelectorAll('button, input, select, textarea, a[href]'),
      ).filter((node) => !node.disabled)

      if (focusable.length === 0) return

      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = previousOverflow
      previouslyFocused?.focus?.()
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <div
      className={styles.dialogOverlay}
      onMouseDown={(event) => {
        if (event.target !== event.currentTarget || busy) return
        onCancel?.()
      }}
    >
      <div
        aria-describedby={description ? descriptionId : undefined}
        aria-labelledby={titleId}
        aria-modal="true"
        className={styles.dialog}
        ref={panelRef}
        role="dialog"
      >
        <h2 className={styles.dialogTitle} id={titleId}>
          {title}
        </h2>

        {description ? (
          <p className={styles.dialogBody} id={descriptionId}>
            {description}
          </p>
        ) : null}

        {children}

        <div className={styles.dialogActions}>
          <button
            className={styles.secondaryButton}
            disabled={busy}
            onClick={onCancel}
            ref={cancelRef}
            type="button"
          >
            {cancelLabel}
          </button>

          <button
            aria-busy={busy ? 'true' : undefined}
            className={tone === 'danger' ? styles.dangerButton : styles.primaryButton}
            disabled={busy}
            onClick={onConfirm}
            type="button"
          >
            {busy ? 'Working…' : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  )
}

export default ConfirmDialog
