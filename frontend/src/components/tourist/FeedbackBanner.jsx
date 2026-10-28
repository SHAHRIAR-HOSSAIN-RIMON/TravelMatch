import styles from './Tourist.module.css'

const toneClassNames = {
  success: styles.bannerSuccess,
  error: styles.bannerError,
  info: styles.bannerInfo,
}

const toneMarks = {
  success: '✓',
  error: '!',
  info: 'i',
}

/**
 * Inline result of a write action (create, update, cancel, accept, register, pay).
 */
function FeedbackBanner({ tone = 'info', children }) {
  if (!children) return null

  const isError = tone === 'error'

  return (
    <div className={`${styles.banner} ${toneClassNames[tone] ?? styles.bannerInfo}`} role={isError ? 'alert' : 'status'}>
      <span aria-hidden="true" className={styles.bannerMark}>
        {toneMarks[tone] ?? toneMarks.info}
      </span>
      <span>{children}</span>
    </div>
  )
}

export default FeedbackBanner
