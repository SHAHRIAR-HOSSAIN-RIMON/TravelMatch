import arrowRightIcon from '../../assets/arrow-right.svg'
import styles from './Auth.module.css'

function AuthSubmitButton({ children, loading, variant = 'login' }) {
  const isSignup = variant === 'signup'

  return (
    <button
      className={`${styles.submitButton} ${isSignup ? styles.submitButtonSignup : ''}`}
      disabled={loading}
      type="submit"
    >
      {loading ? (
        <>
          <span className={styles.spinner} aria-hidden="true" />
          <span>Please wait</span>
        </>
      ) : (
        <>
          <span>{children}</span>
          <img className={styles.submitArrow} src={arrowRightIcon} alt="" />
        </>
      )}
    </button>
  )
}

export default AuthSubmitButton
