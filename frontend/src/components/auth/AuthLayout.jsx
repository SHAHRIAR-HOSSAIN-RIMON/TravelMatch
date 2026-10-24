import { Link } from 'react-router-dom'
import ScenicPanel from './ScenicPanel.jsx'
import styles from './Auth.module.css'

function AuthLayout({ variant = 'login', children }) {
  const isSignup = variant === 'signup'

  return (
    <div className={`${styles.authPage} ${isSignup ? styles.authPageSignup : ''}`}>
      <ScenicPanel variant={variant} />

      <main
        className={`${styles.formPanel} ${isSignup ? styles.formPanelSignup : ''}`}
        aria-label={isSignup ? 'Create your TravelMatch account' : 'Log in to TravelMatch'}
      >
        {!isSignup ? (
          <Link className={styles.backLink} to="/">
            <span aria-hidden="true">←</span> Back to Home
          </Link>
        ) : null}

        <div className={`${styles.formBody} ${isSignup ? styles.formBodySignup : ''}`}>
          {children}
        </div>

        <p className={styles.copyright}>© 2026 TravelMatch. All rights reserved.</p>
      </main>
    </div>
  )
}

export default AuthLayout
