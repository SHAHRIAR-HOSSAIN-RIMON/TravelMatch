import styles from './Guide.module.css'

export function VerificationBadge({ status = 'Pending', showDetail = false }) {
  const normalized = String(status).toLowerCase()

  let badgeClass = styles.badgePending
  let label = 'Verification Pending'
  let detailText = 'Your guide credentials are under administrative review. Proposal submission is unlocked once approved.'

  if (normalized === 'verified') {
    badgeClass = styles.badgeVerified
    label = 'Verified Guide'
    detailText = 'Your guide profile is fully verified. You can submit proposals on all open trip requests.'
  } else if (normalized === 'rejected') {
    badgeClass = styles.badgeRejected
    label = 'Verification Declined'
    detailText = 'Your guide application was not approved. Please contact support or update your profile.'
  }

  return (
    <div className={styles.verificationBadgeWrap}>
      <span className={`${styles.verificationBadge} ${badgeClass}`}>
        <span className={styles.badgeDot} aria-hidden="true" />
        {label}
      </span>
      {showDetail ? <p className={styles.verificationDetail}>{detailText}</p> : null}
    </div>
  )
}

export default VerificationBadge

