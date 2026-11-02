import { Link } from 'react-router-dom'
import styles from './Dashboard.module.css'

function WelcomePanel({ fullName, sentence }) {
  return (
    <section className={styles.welcome} aria-labelledby="dashboard-greeting">
      <div className={styles.welcomeCopy}>
        <h1 className={styles.welcomeHeading} id="dashboard-greeting">
          Welcome back, <span>{fullName}</span>
        </h1>
        <p className={styles.welcomeSentence}>{sentence}</p>
      </div>

      <div className={styles.welcomeAction}>
        <Link className={styles.createButton} to="/trip-requests/create">
          <span className={styles.createIcon} aria-hidden="true" />
          New Trip Request
        </Link>

        <div className={styles.quickActions}>
          <Link className={styles.secondaryActionButton} to="/payments">
            Payments & Escrow
          </Link>
          <Link className={styles.secondaryActionButton} to="/registrations">
            My Registrations
          </Link>
        </div>
      </div>
    </section>
  )
}

export default WelcomePanel

