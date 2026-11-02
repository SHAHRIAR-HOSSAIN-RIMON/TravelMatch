import styles from '../tourist/Tourist.module.css'

export default function ApplicationPagination({ page, totalPages, onChange }) {
  if (totalPages < 2) return null
  return <nav className={styles.formActions} aria-label="Application pages">
    <button className={styles.secondaryButton} disabled={page <= 1} onClick={() => onChange(page - 1)} type="button">Previous</button>
    <span>Page {page} of {totalPages}</span>
    <button className={styles.secondaryButton} disabled={page >= totalPages} onClick={() => onChange(page + 1)} type="button">Next</button>
  </nav>
}
