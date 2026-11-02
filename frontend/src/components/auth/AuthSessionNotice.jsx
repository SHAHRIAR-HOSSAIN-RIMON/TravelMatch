import { useEffect } from 'react'
import { useAuth } from '../../context/useAuth.js'
import AuthLayout from './AuthLayout.jsx'
import AuthFeedback from './AuthFeedback.jsx'
import AuthSubmitButton from './AuthSubmitButton.jsx'
import styles from './Auth.module.css'

export default function AuthSessionNotice() {
  const { isLoading, authError, retrySession, logout } = useAuth()
  return <AuthLayout variant="login">
    <div className={styles.intro}>
      <h1 className={styles.title}>{isLoading ? 'Checking your session…' : 'Let’s reconnect'}</h1>
      <p className={styles.subtitle} role="status">{isLoading ? 'Restoring your saved login. This may take a moment.' : 'Your saved session could not be checked.'}</p>
    </div>
    {authError ? <AuthFeedback tone="error" message="Unable to restore your session" detail={authError} /> : null}
    {!isLoading ? <div className={styles.actions}>
      <AuthSubmitButton onClick={retrySession} type="button">Retry connection</AuthSubmitButton>
      <button className={styles.googleButton} type="button" onClick={logout}>Log in instead</button>
    </div> : null}
  </AuthLayout>
}

export function InvalidSessionRecovery() {
  const { recoverInvalidSession } = useAuth()
  useEffect(() => { recoverInvalidSession() }, [recoverInvalidSession])
  return <AuthSessionNotice />
}
