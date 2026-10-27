import StarRating from './StarRating.jsx'
import styles from './Reviews.module.css'

function formatDate(isoString) {
  if (!isoString) return ''
  try {
    const date = new Date(isoString)
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(date)
  } catch {
    return ''
  }
}

function getInitials(name) {
  if (!name) return 'TM'
  const parts = name.trim().split(/\s+/)
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

export function ReviewCard({ review }) {
  const touristName = review.touristName || 'Verified Tourist'
  const initials = getInitials(touristName)
  const dateFormatted = formatDate(review.createdAt)

  return (
    <article className={styles.reviewCard}>
      <div className={styles.reviewCardHeader}>
        <div className={styles.reviewerMeta}>
          <div className={styles.reviewerAvatar} aria-hidden="true">
            {initials}
          </div>
          <div>
            <div className={styles.reviewerName}>{touristName}</div>
            {dateFormatted && <div className={styles.reviewDate}>{dateFormatted}</div>}
          </div>
        </div>
        <StarRating size={16} value={review.rating} />
      </div>

      {review.comment ? (
        <p className={styles.reviewComment}>{review.comment}</p>
      ) : (
        <p className={styles.reviewComment} style={{ fontStyle: 'italic', color: 'var(--tm-slate-400)' }}>
          No written feedback provided.
        </p>
      )}
    </article>
  )
}

export default ReviewCard

