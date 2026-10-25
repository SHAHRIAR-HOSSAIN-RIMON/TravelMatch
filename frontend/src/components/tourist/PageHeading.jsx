import { Link } from 'react-router-dom'
import styles from './Tourist.module.css'

function PageHeading({ eyebrow, title, description, backTo, backLabel, children }) {
  return (
    <>
      {backTo ? (
        <Link className={styles.backLink} to={backTo}>
          <span className={styles.backIcon} aria-hidden="true" />
          {backLabel ?? 'Back'}
        </Link>
      ) : null}

      <header className={styles.heading}>
        {eyebrow ? <p className={styles.eyebrow}>{eyebrow}</p> : null}
        <h1 className={styles.title}>{title}</h1>
        {description ? <p className={styles.lead}>{description}</p> : null}
        {children ? <div className={styles.headingActions}>{children}</div> : null}
      </header>
    </>
  )
}

export default PageHeading
