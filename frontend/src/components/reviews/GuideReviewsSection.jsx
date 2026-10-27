import { useCallback, useEffect, useRef, useState } from 'react'
import StarRating from './StarRating.jsx'
import ReviewCard from './ReviewCard.jsx'
import { getGuideRating, getGuideReviews } from '../../lib/reviewService.js'
import styles from './Reviews.module.css'

export function GuideReviewsSection({ token, guideProfileId, title = 'Reviews & Ratings' }) {
  const [ratingSummary, setRatingSummary] = useState(null)
  const [reviewsData, setReviewsData] = useState({
    reviews: [],
    totalCount: 0,
    page: 1,
    pageSize: 5,
    totalPages: 0,
  })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const requestIdRef = useRef(0)

  const loadReviews = useCallback(
    async (pageToLoad = 1) => {
      if (!token || !guideProfileId) return

      const requestId = ++requestIdRef.current
      setLoading(true)
      setError(null)

      try {
        const [ratingRes, reviewsRes] = await Promise.allSettled([
          getGuideRating(token, guideProfileId),
          getGuideReviews(token, guideProfileId, pageToLoad, 5),
        ])

        if (requestId !== requestIdRef.current) return

        if (ratingRes.status === 'fulfilled') {
          setRatingSummary(ratingRes.value)
        }

        if (reviewsRes.status === 'fulfilled') {
          const res = reviewsRes.value
          setReviewsData({
            reviews: res.data || [],
            totalCount: res.totalCount || 0,
            page: res.page || pageToLoad,
            pageSize: res.pageSize || 5,
            totalPages: res.totalPages || 0,
          })
        } else {
          setError(reviewsRes.reason?.message || 'Could not load reviews.')
        }
      } catch (err) {
        if (requestId !== requestIdRef.current) return
        setError(err instanceof Error ? err.message : 'Failed to load reviews.')
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false)
        }
      }
    },
    [guideProfileId, token],
  )

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await loadReviews(1)
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [loadReviews])

  const avgRating = ratingSummary?.averageRating
  const reviewCount = ratingSummary?.reviewCount ?? reviewsData.totalCount

  return (
    <section className={styles.reviewsSection} aria-label="Guide reviews section">
      <div className={styles.reviewsHeader}>
        <h3 className={styles.sectionTitle}>{title}</h3>

        {reviewCount > 0 ? (
          <div className={styles.ratingSummaryBadge}>
            <StarRating size={18} value={avgRating ?? 0} />
            <span className={styles.ratingScore}>
              {avgRating !== null && avgRating !== undefined ? Number(avgRating).toFixed(1) : '—'}
            </span>
            <span className={styles.reviewCountText}>
              ({reviewCount} review{reviewCount === 1 ? '' : 's'})
            </span>
          </div>
        ) : (
          <span className={styles.reviewCountText}>No reviews yet</span>
        )}
      </div>

      {error ? (
        <div className={styles.errorMessage}>{error}</div>
      ) : loading ? (
        <div className={styles.reviewList}>
          <div className={styles.loadingSkeleton} />
          <div className={styles.loadingSkeleton} />
        </div>
      ) : reviewsData.reviews.length === 0 ? (
        <div className={styles.emptyReviews}>
          <p style={{ margin: 0 }}>This guide does not have any reviews yet.</p>
        </div>
      ) : (
        <>
          <div className={styles.reviewList}>
            {reviewsData.reviews.map((review) => (
              <ReviewCard key={review.id} review={review} />
            ))}
          </div>

          {reviewsData.totalPages > 1 && (
            <div className={styles.pagination}>
              <button
                className={styles.pageButton}
                disabled={reviewsData.page <= 1 || loading}
                onClick={() => loadReviews(reviewsData.page - 1)}
                type="button"
              >
                ← Previous
              </button>

              <span className={styles.pageInfo}>
                Page {reviewsData.page} of {reviewsData.totalPages}
              </span>

              <button
                className={styles.pageButton}
                disabled={reviewsData.page >= reviewsData.totalPages || loading}
                onClick={() => loadReviews(reviewsData.page + 1)}
                type="button"
              >
                Next →
              </button>
            </div>
          )}
        </>
      )}
    </section>
  )
}

export default GuideReviewsSection

