const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateEmail(email) {
  const value = email.trim()

  if (!value) return 'Enter your email address.'
  if (!EMAIL_PATTERN.test(value)) return 'Enter a valid email address.'

  return ''
}

export function validateLogin({ email, password }) {
  const errors = {}
  const emailError = validateEmail(email)
  if (emailError) errors.email = emailError
  if (!password) errors.password = 'Enter your password.'

  return errors
}

export function validateSignup({ fullName, email, phoneNumber, password, confirmPassword, role }) {
  const errors = {}
  const emailError = validateEmail(email)

  if (!fullName.trim()) {
    errors.fullName = 'Enter your full name.'
  } else if (fullName.trim().length < 2) {
    errors.fullName = 'Enter at least 2 characters.'
  } else if (fullName.trim().length > 100) {
    errors.fullName = 'Full name must be 100 characters or fewer.'
  }

  if (emailError) errors.email = emailError

  if (!phoneNumber.trim()) {
    errors.phoneNumber = 'Enter your phone number.'
  } else if (phoneNumber.trim().length > 20) {
    errors.phoneNumber = 'Phone number must be 20 characters or fewer.'
  }

  if (!password) {
    errors.password = 'Create a password.'
  } else if (password.length < 8) {
    errors.password = 'Use at least 8 characters.'
  }

  if (!confirmPassword) {
    errors.confirmPassword = 'Confirm your password.'
  } else if (confirmPassword !== password) {
    errors.confirmPassword = 'Passwords do not match.'
  }

  if (!role) errors.role = 'Select your role.'

  return errors
}
