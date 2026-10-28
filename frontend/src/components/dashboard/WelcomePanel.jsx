import { Link } from 'react-router-dom'
import styles from './Dashboard.module.css'

function WelcomePanel({ fullName, sentence }) {
  return (
    <section className={styles.welcome} aria-labelledby="dashboard-greeting">
      <div className={styles.welcomeCopy}>
        <h1 className={styles.welcomeHeading} id="dashboard-greeting">
          Welcome, {fullName}
        </h1>
        <p className={styles.welcomeSentence}>{sentence}</p>
      </div>

      <div className={styles.welcomeAction}>
        <Link className={styles.createButton} to="/trip-requests/create">
          <span className={styles.createIcon} aria-hidden="true" />
          Create Trip Request
        </Link>
      </div>
    </section>
  )
}

export default WelcomePanel

