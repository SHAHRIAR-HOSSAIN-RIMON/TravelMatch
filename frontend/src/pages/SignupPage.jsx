import { useState } from 'react'
import { Link } from 'react-router-dom'
import AuthFeedback from '../components/auth/AuthFeedback.jsx'
import AuthLayout from '../components/auth/AuthLayout.jsx'
import AuthSubmitButton from '../components/auth/AuthSubmitButton.jsx'
import AuthTextField from '../components/auth/AuthTextField.jsx'
import RoleSelect from '../components/auth/RoleSelect.jsx'
import { submitSignup } from '../lib/authService.js'
import { validateSignup } from '../lib/validation.js'
import styles from '../components/auth/Auth.module.css'

const initialValues = {
  role: 'tourist',
  fullName: '',
  email: '',
  phoneNumber: '',
  password: '',
  confirmPassword: '',
}

function SignupPage() {
  const [values, setValues] = useState(initialValues)
  const [errors, setErrors] = useState({})
  const [status, setStatus] = useState('idle')
  const [feedback, setFeedback] = useState(null)

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
    const nextErrors = validateSignup(values)
    setErrors(nextErrors)

    if (Object.keys(nextErrors).length > 0) {
      setStatus('idle')
      setFeedback({
        tone: 'error',
        message: 'Check the highlighted fields',
        detail: 'Complete your name, email, password and role to create an account.',
      })
      if (nextErrors.role) {
        document.getElementById('signup-role-options')?.querySelector('[role="radio"]')?.focus()
      } else {
        document.getElementById(Object.keys(nextErrors)[0])?.focus()
      }
      return
    }

    setStatus('loading')
    setFeedback(null)

    try {
      const result = await submitSignup(values)
      if (result.success && result.data) {
        setStatus('success')
        setFeedback({
          tone: 'success',
          message: 'Account created successfully',
          detail: 'Your account is ready. You can now log in.',
        })
      } else {
        setStatus('error')
        setFeedback({
          tone: 'error',
          message: result.message || 'Registration failed',
          detail: 'Please check your information and try again.',
        })
      }
    } catch (err) {
      setStatus('error')
      const message = err.data && (err.data.message || err.data.Message || err.data.error || err.data.Error)
      setFeedback({
        tone: 'error',
        message: message || "We couldn't submit your details",
        detail: 'Please try again in a moment.',
      })
    }
  }

  return (
    <AuthLayout variant="signup">
      <div className={styles.intro}>
        <h1 className={`${styles.title} ${styles.titleSignup}`}>Create Your Account</h1>
        <p className={styles.subtitle}>Join over 50,000 travelers exploring the world authentically.</p>
      </div>

      <form
        aria-busy={status === 'loading'}
        className={`${styles.authForm} ${styles.authFormSignup}`}
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

        <RoleSelect
          error={errors.role}
          onChange={(role) => updateField('role', role)}
          value={values.role}
        />

        <div className={`${styles.fields} ${styles.fieldsSignup}`}>
          <AuthTextField
            autoComplete="name"
            error={errors.fullName}
            id="fullName"
            label="Full Name"
            onChange={(event) => updateField('fullName', event.target.value)}
            placeholder="e.g. Sarah Jenkins"
            variant="signup"
            value={values.fullName}
          />
          <AuthTextField
            autoComplete="email"
            error={errors.email}
            id="email"
            inputMode="email"
            label="Email Address"
            onChange={(event) => updateField('email', event.target.value)}
            placeholder="you@example.com"
            type="email"
            variant="signup"
            value={values.email}
          />
          <AuthTextField
            autoComplete="tel"
            error={errors.phoneNumber}
            id="phoneNumber"
            inputMode="tel"
            label="Phone Number"
            onChange={(event) => updateField('phoneNumber', event.target.value)}
            placeholder="+8801XXXXXXXXX"
            type="tel"
            variant="signup"
            value={values.phoneNumber}
          />
          <AuthTextField
            autoComplete="new-password"
            error={errors.password}
            id="password"
            label="Password"
            onChange={(event) => updateField('password', event.target.value)}
            placeholder="••••••••"
            type="password"
            variant="signup"
            value={values.password}
          />
          <AuthTextField
            autoComplete="new-password"
            error={errors.confirmPassword}
            id="confirmPassword"
            label="Confirm Password"
            onChange={(event) => updateField('confirmPassword', event.target.value)}
            placeholder="••••••••"
            type="password"
            variant="signup"
            value={values.confirmPassword}
          />
        </div>

        <div className={styles.actionsSignup}>
          <AuthSubmitButton loading={status === 'loading'} variant="signup">
            Create Account
          </AuthSubmitButton>

          <p className={`${styles.switchAuth} ${styles.switchAuthSignup}`}>
            Already have an account? <Link to="/login">Log In</Link>
          </p>
        </div>
      </form>
    </AuthLayout>
  )
}

export default SignupPage
