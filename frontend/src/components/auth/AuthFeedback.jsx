import styles from './Auth.module.css'

const toneStyles = {
  success: styles.feedbackSuccess,
  error: styles.feedbackError,
  info: styles.feedbackInfo,
}

function AuthFeedback({ tone = 'info', message, detail }) {
  return (
    <div
      className={`${styles.feedback} ${toneStyles[tone]}`}
      role={tone === 'error' ? 'alert' : 'status'}
    >
      <span className={styles.feedbackMarker} aria-hidden="true" />
      <span>
        <strong className={styles.feedbackMessage}>{message}</strong>
        {detail ? <span className={styles.feedbackDetail}>{detail}</span> : null}
      </span>
    </div>
  )
}

export default AuthFeedback
