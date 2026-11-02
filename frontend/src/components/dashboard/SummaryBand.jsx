import styles from './Dashboard.module.css'

const ACCENT_COLORS = [
  'linear-gradient(90deg, #0d9488, #059b86)',      /* teal — requests */
  'linear-gradient(90deg, #6366f1, #818cf8)',      /* indigo — proposals */
  'linear-gradient(90deg, #f59e0b, #d97706)',      /* amber — escrow */
  'linear-gradient(90deg, #10b981, #059669)',      /* emerald — fourth */
]

function SummaryBand({ items, onRetryResource }) {
  const isBusy = items.some((item) => item.state === 'loading')

  return (
    <section className={styles.summaryBand} aria-busy={isBusy} aria-label="Your trip activity">
      {items.map((item, index) => (
        <div
          className={styles.summaryItem}
          key={item.id}
          style={{ '--item-accent': ACCENT_COLORS[index % ACCENT_COLORS.length] }}
        >
          <p className={styles.summaryLabel}>{item.label}</p>

          {item.state === 'loading' ? (
            <>
              <span className={styles.summaryPlaceholder} aria-hidden="true" />
              <span className={styles.srOnly}>Loading {item.label.toLowerCase()}</span>
            </>
          ) : null}

          {item.state === 'error' ? (
            <>
              <p className={styles.summaryValueSmall}>Couldn&apos;t load</p>
              <p className={styles.summaryQualifier}>{item.qualifier}</p>
              {item.retryResource ? (
                <button
                  className={styles.summaryRetry}
                  onClick={() => onRetryResource(item.retryResource)}
                  type="button"
                >
                  Retry {item.retryLabel}
                </button>
              ) : null}
            </>
          ) : null}

          {item.state === 'ready' ? (
            <>
              <p className={item.emphasis === 'text' ? styles.summaryValueText : styles.summaryValue}>
                {item.value}
              </p>
              <p className={styles.summaryQualifier}>{item.qualifier}</p>
            </>
          ) : null}
        </div>
      ))}
    </section>
  )
}

export default SummaryBand

