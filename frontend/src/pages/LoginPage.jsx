import { useState } from 'react'
import { Link } from 'react-router-dom'
import AuthFeedback from '../components/auth/AuthFeedback.jsx'
import AuthLayout from '../components/auth/AuthLayout.jsx'
import AuthSubmitButton from '../components/auth/AuthSubmitButton.jsx'
import AuthTextField from '../components/auth/AuthTextField.jsx'
import checkIcon from '../assets/auth-check.svg'
import googleIcon from '../assets/auth-google.svg'
import { validateLogin } from '../lib/validation.js'
import { ApiError } from '../lib/api.js'
import { useAuth } from '../context/useAuth.js'
import styles from '../components/auth/Auth.module.css'

const initialValues = {
  email: '',
  password: '',
  remember: true,
}

function LoginPage() {
  const [values, setValues] = useState(initialValues)
  const [errors, setErrors] = useState({})
  const [status, setStatus] = useState('idle')
  const [feedback, setFeedback] = useState(null)
  const { login } = useAuth()

  function updateField(field, value) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => {
      if (!current[field]) return current
      const next = { ...current }
      delete next[field]
      return next
    })
    if (status !== 'loading') setFeedback(null)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    const nextErrors = validateLogin(values)
    setErrors(nextErrors)

    if (Object.keys(nextErrors).length > 0) {
      setStatus('idle')
      setFeedback({
        tone: 'error',
        message: 'Check the highlighted fields',
        detail: 'Enter a valid email address and your password to continue.',
      })
      document.getElementById(Object.keys(nextErrors)[0])?.focus()
      return
    }

    setStatus('loading')
    setFeedback(null)

    try {
      await login({ email: values.email, password: values.password, remember: values.remember })
      setStatus('success')
      setFeedback({
        tone: 'success',
        message: 'Logged in successfully',
        detail: 'Welcome back.',
      })
    } catch (error) {
      setStatus('error')
      if (error instanceof ApiError) {
        switch (error.status) {
          case 401:
            setFeedback({
              tone: 'error',
              message: 'Invalid email or password',
              detail: 'Please check your credentials and try again.',
            })
            break
          case 403:
            setFeedback({
              tone: 'error',
              message: 'Account inactive',
              detail: 'This account has been deactivated. Contact support for assistance.',
            })
            break
          default:
            setFeedback({
              tone: 'error',
              message: error.message || 'Login failed',
              detail: 'Please try again in a moment.',
            })
        }
      } else {
        setFeedback({
          tone: 'error',
          message: "We couldn't submit your details",
          detail: 'Please try again in a moment.',
        })
      }
    }
  }

  function showInfo(message) {
    setFeedback({ tone: 'info', message })
  }

  return (
    <AuthLayout variant="login">
      <div className={styles.intro}>
        <h1 className={styles.title}>Welcome Back</h1>
        <p className={styles.subtitle}>Log in to continue your travel journey with TravelMatch.</p>
      </div>

      <form
        aria-busy={status === 'loading'}
        className={styles.authForm}
        noValidate
        onSubmit={handleSubmit}
      >
        {feedback ? (
          <AuthFeedback
            detail={feedback.detail}
            message={feedback.message}
            tone={feedback.tone}
          />
        ) : null}

        <div className={styles.fields}>
          <AuthTextField
            autoComplete="email"
            error={errors.email}
            icon="mail"
            id="email"
            inputMode="email"
            label="Email Address"
            onChange={(event) => updateField('email', event.target.value)}
            placeholder="Enter your email address"
            type="email"
            value={values.email}
          />
          <AuthTextField
            autoComplete="current-password"
            error={errors.password}
            icon="lock"
            id="password"
            label="Password"
            onChange={(event) => updateField('password', event.target.value)}
            placeholder="Enter your password"
            type="password"
            value={values.password}
          />
        </div>

        <div className={styles.optionsRow}>
          <label className={styles.checkbox} htmlFor="remember-me">
            <input
              checked={values.remember}
              id="remember-me"
              onChange={(event) => updateField('remember', event.target.checked)}
              type="checkbox"
            />
            <span className={styles.checkboxBox} aria-hidden="true">
              <img src={checkIcon} alt="" />
            </span>
            Remember me
          </label>

          <button
            className={styles.textLink}
            onClick={() => showInfo('Password reset is not connected yet. Ask your TravelMatch administrator for help.')}
            type="button"
          >
            Forgot password?
          </button>
        </div>

        <div className={styles.actions}>
          <AuthSubmitButton loading={status === 'loading'}>Log In</AuthSubmitButton>

          <div className={styles.divider}>
            <span className={styles.dividerLine} aria-hidden="true" />
            <span>or</span>
            <span className={styles.dividerLine} aria-hidden="true" />
          </div>

          <button
            className={styles.googleButton}
            onClick={() => showInfo('Google sign-in is not connected yet. Use your email and password instead.')}
            type="button"
          >
            <img src={googleIcon} alt="" />
            Continue with Google
          </button>
        </div>

        <p className={styles.switchAuth}>
          Don&apos;t have an account? <Link to="/signup">Sign Up</Link>
        </p>
      </form>
    </AuthLayout>
  )
}

export default LoginPage
