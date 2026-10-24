import { useState } from 'react'
import eyeOffIcon from '../../assets/auth-eye-off.svg'
import eyeIcon from '../../assets/auth-eye.svg'
import lockIcon from '../../assets/auth-lock.svg'
import mailIcon from '../../assets/auth-mail.svg'
import styles from './Auth.module.css'

const icons = {
  mail: mailIcon,
  lock: lockIcon,
}

function AuthTextField({
  id,
  label,
  type = 'text',
  placeholder,
  value,
  onChange,
  onBlur,
  error,
  icon,
  autoComplete,
  inputMode,
  variant = 'login',
}) {
  const [revealed, setRevealed] = useState(false)
  const isPassword = type === 'password'
  const inputType = isPassword && revealed ? 'text' : type
  const isSignup = variant === 'signup'
  const errorId = `${id}-error`
  const hiddenIcon = isSignup ? eyeIcon : eyeOffIcon
  const revealedIcon = isSignup ? eyeOffIcon : eyeIcon

  return (
    <div className={`${styles.field} ${isSignup ? styles.fieldSignup : ''}`}>
      <label className={`${styles.label} ${isSignup ? styles.labelSignup : ''}`} htmlFor={id}>
        {label}
      </label>

      <div
        className={`${styles.inputShell} ${isSignup ? styles.inputShellSignup : ''} ${
          error ? styles.inputShellError : ''
        }`}
      >
        {icon ? <img className={styles.inputIcon} src={icons[icon]} alt="" /> : null}

        <input
          aria-describedby={error ? errorId : undefined}
          aria-invalid={error ? 'true' : undefined}
          autoComplete={autoComplete}
          className={`${styles.input} ${isSignup ? styles.inputSignup : ''}`}
          id={id}
          inputMode={inputMode}
          name={id}
          onBlur={onBlur}
          onChange={onChange}
          placeholder={placeholder}
          type={inputType}
          value={value}
        />

        {isPassword ? (
          <button
            aria-label={revealed ? 'Hide password' : 'Show password'}
            aria-pressed={revealed}
            className={styles.revealButton}
            onClick={() => setRevealed((current) => !current)}
            type="button"
          >
            <img src={revealed ? revealedIcon : hiddenIcon} alt="" />
          </button>
        ) : null}
      </div>

      {error ? (
        <p className={styles.fieldError} id={errorId} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  )
}

export default AuthTextField
