import { useState } from 'react'
import styles from './Reviews.module.css'

export function StarRating({
  value = 0,
  max = 5,
  interactive = false,
  onChange,
  size = 18,
}) {
  const [hoverValue, setHoverValue] = useState(null)
  const displayValue = hoverValue !== null ? hoverValue : Math.round(value || 0)

  return (
    <div
      aria-label={`Rating: ${value || 0} out of ${max} stars`}
      className={styles.starsContainer}
      role={interactive ? 'radiogroup' : 'img'}
    >
      {Array.from({ length: max }, (_, index) => {
        const starNumber = index + 1
        const isFilled = starNumber <= displayValue

        if (interactive) {
          return (
            <button
              aria-checked={value === starNumber}
              aria-label={`${starNumber} star${starNumber > 1 ? 's' : ''}`}
              className={`${styles.starInteractive} ${isFilled ? styles.starFilled : styles.star}`}
              key={starNumber}
              onClick={() => onChange?.(starNumber)}
              onMouseEnter={() => setHoverValue(starNumber)}
              onMouseLeave={() => setHoverValue(null)}
              role="radio"
              style={{ fontSize: `${size}px` }}
              type="button"
            >
              ★
            </button>
          )
        }

        return (
          <span
            aria-hidden="true"
            className={`${styles.star} ${isFilled ? styles.starFilled : ''}`}
            key={starNumber}
            style={{ fontSize: `${size}px` }}
          >
            ★
          </span>
        )
      })}
    </div>
  )
}

export default StarRating

