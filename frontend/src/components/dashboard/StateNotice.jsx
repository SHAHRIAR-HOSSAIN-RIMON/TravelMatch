import styles from './Dashboard.module.css'

function StateNotice({
  tone = 'neutral',
  title,
  detail,
  actionLabel,
  onAction,
  className = '',
  children,
}) {
  const isError = tone === 'error'

  return (
    <div
      className={`${styles.notice} ${isError ? styles.noticeError : ''} ${className}`}
      role={isError ? 'alert' : 'status'}
    >
      <p className={styles.noticeTitle}>{title}</p>
      {detail ? <p className={styles.noticeDetail}>{detail}</p> : null}

      {onAction ? (
        <button className={styles.retryButton} onClick={onAction} type="button">
          <span className={styles.retryIcon} aria-hidden="true" />
          {actionLabel}
        </button>
      ) : null}

      {children}
    </div>
  )
}

export default StateNotice
