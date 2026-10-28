import StatusBadge from './StatusBadge.jsx'
import { formatDateRange } from './dashboardModel.js'
import styles from './Dashboard.module.css'

function requestTitle(request) {
  const destination = typeof request.destination === 'string' ? request.destination.trim() : ''
  return destination || `Trip request #${request.id}`
}

function RequestList({ requests, loading, selectedRequestId, onSelect, headingRef }) {
  return (
    <section className={styles.requestList} aria-labelledby="request-list-heading">
      <div className={styles.listHeader}>
        <h2 className={styles.sectionTitle} id="request-list-heading" ref={headingRef} tabIndex={-1}>
          Recent trip requests
        </h2>
        {!loading && requests.length > 0 ? (
          <span aria-hidden="true" className={styles.listCount}>
            {requests.length}
          </span>
        ) : null}
      </div>

      {loading ? (
        <ul className={styles.requestRows} aria-hidden="true">
          {[0, 1, 2].map((row) => (
            <li className={styles.rowSkeleton} key={row}>
              <span className={styles.rowSkeletonMain}>
                <span className={`${styles.skeletonBlock} ${styles.skeletonLineTitle}`} />
                <span className={`${styles.skeletonBlock} ${styles.skeletonLineMeta}`} />
              </span>
              <span className={`${styles.skeletonBlock} ${styles.skeletonBadge}`} />
            </li>
          ))}
        </ul>
      ) : (
        <ul className={styles.requestRows}>
          {requests.map((request) => {
            const isSelected = request.id === selectedRequestId

            return (
              <li key={request.id}>
                <button
                  aria-controls="request-detail"
                  aria-current={isSelected ? 'true' : undefined}
                  className={`${styles.requestRow} ${isSelected ? styles.requestRowSelected : ''}`}
                  onClick={() => onSelect(request.id)}
                  type="button"
                >
                  <span className={styles.requestRowTitle}>{requestTitle(request)}</span>

                  <span className={styles.requestRowStatus}>
                    <StatusBadge status={request.status} />
                  </span>

                  <span className={styles.requestRowDates}>
                    {formatDateRange(request.startDate, request.endDate)}
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

export default RequestList
