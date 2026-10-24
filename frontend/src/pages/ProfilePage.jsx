import { useState, useEffect } from 'react'
import { useAuth } from '../context/useAuth.js'
import { getProfile, updateProfile } from '../lib/profileService.js'
import { ApiError } from '../lib/api.js'
import AuthLayout from '../components/auth/AuthLayout.jsx'
import AuthTextField from '../components/auth/AuthTextField.jsx'
import AuthSubmitButton from '../components/auth/AuthSubmitButton.jsx'
import AuthFeedback from '../components/auth/AuthFeedback.jsx'
import styles from '../components/auth/Auth.module.css'

function ProfilePage() {
  const { token, isAuthenticated, isLoading: authLoading } = useAuth()
  const [profile, setProfile] = useState(null)
  const [errors, setErrors] = useState({})
  const [feedback, setFeedback] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isEditing, setIsEditing] = useState(false)
  const [formData, setFormData] = useState({})

  useEffect(() => {
    async function fetchProfile() {
      if (!isAuthenticated || !token) {
        setIsLoading(false)
        return
      }
      try {
        const data = await getProfile(token)
        setProfile(data)
        setFormData({
          fullName: data.fullName ?? '',
          phoneNumber: data.phoneNumber ?? '',
          preferences: data.touristProfile?.preferences ?? '',
          bio: data.guideProfile?.bio ?? '',
          serviceArea: data.guideProfile?.serviceArea ?? '',
          experienceYears: data.guideProfile?.experienceYears ?? '',
          supportedTripTypes: data.guideProfile?.supportedTripTypes ?? '',
          photoUrl: data.guideProfile?.photoUrl ?? '',
          groupOrganizationName: data.organizerProfile?.groupOrganizationName ?? '',
        })
      } catch (error) {
        setFeedback({
          tone: 'error',
          message: 'Failed to load profile',
          detail: error instanceof ApiError ? error.message : 'Please try again later.',
        })
      } finally {
        setIsLoading(false)
      }
    }
    fetchProfile()
  }, [isAuthenticated, token])

  function handleChange(field, value) {
    setFormData((prev) => ({ ...prev, [field]: value }))
    setErrors((prev) => {
      if (!prev[field]) return prev
      const next = { ...prev }
      delete next[field]
      return next
    })
  }

  function validateForm() {
    const newErrors = {}
    if (!formData.fullName?.trim()) {
      newErrors.fullName = 'Full name is required.'
    } else if (formData.fullName.trim().length > 100) {
      newErrors.fullName = 'Full name must be 100 characters or fewer.'
    }
    if (formData.phoneNumber && formData.phoneNumber.trim().length > 20) {
      newErrors.phoneNumber = 'Phone number must be 20 characters or fewer.'
    }
    if (formData.experienceYears && (isNaN(formData.experienceYears) || Number(formData.experienceYears) < 0)) {
      newErrors.experienceYears = 'Experience years must be a positive number.'
    }
    if (formData.preferences && formData.preferences.length > 1000) {
      newErrors.preferences = 'Preferences must be 1000 characters or fewer.'
    }
    if (formData.bio && formData.bio.length > 2000) {
      newErrors.bio = 'Bio must be 2000 characters or fewer.'
    }
    if (formData.serviceArea && formData.serviceArea.length > 100) {
      newErrors.serviceArea = 'Service area must be 100 characters or fewer.'
    }
    if (formData.supportedTripTypes && formData.supportedTripTypes.length > 500) {
      newErrors.supportedTripTypes = 'Supported trip types must be 500 characters or fewer.'
    }
    if (formData.photoUrl && formData.photoUrl.length > 500) {
      newErrors.photoUrl = 'Photo URL must be 500 characters or fewer.'
    }
    if (formData.groupOrganizationName && formData.groupOrganizationName.length > 253) {
      newErrors.groupOrganizationName = 'Organization name must be 253 characters or fewer.'
    }
    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  async function handleSave(event) {
    event.preventDefault()
    if (!validateForm()) return

    setFeedback(null)
    try {
      const payload = {}
      if (formData.fullName !== profile?.fullName) payload.fullName = formData.fullName.trim()
      if (formData.phoneNumber !== profile?.phoneNumber) payload.phoneNumber = formData.phoneNumber.trim()
      if (formData.preferences !== profile?.touristProfile?.preferences) payload.preferences = formData.preferences
      if (formData.bio !== profile?.guideProfile?.bio) payload.bio = formData.bio
      if (formData.serviceArea !== profile?.guideProfile?.serviceArea) payload.serviceArea = formData.serviceArea
      if (formData.experienceYears !== String(profile?.guideProfile?.experienceYears ?? '')) {
        payload.experienceYears = formData.experienceYears ? Number(formData.experienceYears) : null
      }
      if (formData.supportedTripTypes !== profile?.guideProfile?.supportedTripTypes) payload.supportedTripTypes = formData.supportedTripTypes
      if (formData.photoUrl !== profile?.guideProfile?.photoUrl) payload.photoUrl = formData.photoUrl
      if (formData.groupOrganizationName !== profile?.organizerProfile?.groupOrganizationName) payload.groupOrganizationName = formData.groupOrganizationName

      if (Object.keys(payload).length === 0) {
        setFeedback({ tone: 'info', message: 'No changes to save.' })
        setIsEditing(false)
        return
      }

      const updated = await updateProfile(token, payload)
      setProfile(updated)
      setFeedback({ tone: 'success', message: 'Profile updated', detail: 'Your changes have been saved.' })
      setIsEditing(false)
    } catch (error) {
      setFeedback({
        tone: 'error',
        message: 'Failed to update profile',
        detail: error instanceof ApiError ? error.message : 'Please try again later.',
      })
    }
  }

  function handleCancel() {
    setFormData({
      fullName: profile?.fullName ?? '',
      phoneNumber: profile?.phoneNumber ?? '',
      preferences: profile?.touristProfile?.preferences ?? '',
      bio: profile?.guideProfile?.bio ?? '',
      serviceArea: profile?.guideProfile?.serviceArea ?? '',
      experienceYears: profile?.guideProfile?.experienceYears ?? '',
      supportedTripTypes: profile?.guideProfile?.supportedTripTypes ?? '',
      photoUrl: profile?.guideProfile?.photoUrl ?? '',
      groupOrganizationName: profile?.organizerProfile?.groupOrganizationName ?? '',
    })
    setErrors({})
    setFeedback(null)
    setIsEditing(false)
  }

  if (authLoading || isLoading) {
    return (
      <AuthLayout variant="signup">
        <div className={styles.intro}>
          <div className={styles.spinner} aria-hidden="true" />
          <p>Loading profile...</p>
        </div>
      </AuthLayout>
    )
  }

  if (!isAuthenticated || !profile) {
    return (
      <AuthLayout variant="signup">
        <div className={styles.intro}>
          <h1 className={styles.title}>Profile</h1>
          <p>Please log in to view your profile.</p>
        </div>
      </AuthLayout>
    )
  }

  const role = profile.role
  const isTourist = role === 'Tourist'
  const isGuide = role === 'Guide'
  const isOrganizer = role === 'Organizer'

  const commonFields = [
    { id: 'fullName', label: 'Full Name', type: 'text', autoComplete: 'name', editable: true, value: formData.fullName },
    { id: 'email', label: 'Email', type: 'email', autoComplete: 'email', editable: false, value: profile.email },
    { id: 'phoneNumber', label: 'Phone Number', type: 'tel', autoComplete: 'tel', editable: true, value: formData.phoneNumber },
    { id: 'role', label: 'Role', type: 'text', autoComplete: 'off', editable: false, value: role },
  ]

  const roleFields = isTourist
    ? [
        { id: 'preferences', label: 'Preferences', type: 'text', autoComplete: 'off', editable: true, value: formData.preferences, multiline: true },
      ]
    : isGuide
    ? [
        { id: 'bio', label: 'Bio', type: 'text', autoComplete: 'off', editable: true, value: formData.bio, multiline: true },
        { id: 'serviceArea', label: 'Service Area', type: 'text', autoComplete: 'off', editable: true, value: formData.serviceArea },
        { id: 'experienceYears', label: 'Experience (Years)', type: 'number', autoComplete: 'off', editable: true, value: formData.experienceYears },
        { id: 'supportedTripTypes', label: 'Supported Trip Types', type: 'text', autoComplete: 'off', editable: true, value: formData.supportedTripTypes, multiline: true },
        { id: 'photoUrl', label: 'Photo URL', type: 'url', autoComplete: 'url', editable: true, value: formData.photoUrl },
      ]
    : isOrganizer
    ? [
        { id: 'groupOrganizationName', label: 'Group/Organization Name', type: 'text', autoComplete: 'organization', editable: true, value: formData.groupOrganizationName },
        { id: 'bio', label: 'Bio', type: 'text', autoComplete: 'off', editable: true, value: formData.bio, multiline: true },
      ]
    : []

  return (
    <AuthLayout variant="signup">
      <div className={styles.intro}>
        <h1 className={`${styles.title} ${styles.titleSignup}`}>Your Profile</h1>
        <p className={styles.subtitle}>Manage your account information.</p>
      </div>

      <div className={`${styles.authForm} ${styles.authFormSignup}`}>
        {feedback ? (
          <AuthFeedback detail={feedback.detail} message={feedback.message} tone={feedback.tone} />
        ) : null}

        <div className={`${styles.fields} ${styles.fieldsSignup}`}>
          {commonFields.map((field) => (
            <AuthTextField
              key={field.id}
              id={field.id}
              label={field.label}
              type={field.type}
              inputMode={field.type === 'tel' ? 'tel' : field.type === 'number' ? 'numeric' : undefined}
              autoComplete={field.autoComplete}
              value={field.value ?? ''}
              onChange={(e) => handleChange(field.id, e.target.value)}
              error={errors[field.id]}
              variant="signup"
              disabled={!field.editable || !isEditing}
            />
          ))}

          {roleFields.map((field) => (
            <AuthTextField
              key={field.id}
              id={field.id}
              label={field.label}
              type={field.type}
              inputMode={field.type === 'number' ? 'numeric' : undefined}
              autoComplete={field.autoComplete}
              value={field.value ?? ''}
              onChange={(e) => handleChange(field.id, e.target.value)}
              error={errors[field.id]}
              variant="signup"
              disabled={!field.editable || !isEditing}
              multiline={field.multiline}
            />
          ))}

          {!isEditing ? (
            <AuthSubmitButton variant="signup" onClick={() => setIsEditing(true)}>
              Edit Profile
            </AuthSubmitButton>
          ) : (
            <div className={styles.actionsSignup}>
              <AuthSubmitButton variant="signup" loading={false} onClick={handleSave}>
                Save Changes
              </AuthSubmitButton>
              <button
                type="button"
                className={`${styles.submitButton} ${styles.submitButtonSignup}`}
                onClick={handleCancel}
              >
                Cancel
              </button>
            </div>
          )}
        </div>
      </div>
    </AuthLayout>
  )
}

export default ProfilePage