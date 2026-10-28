import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/useAuth.js'
import { getProfile, updateProfile } from '../lib/profileService.js'
import { ApiError } from '../lib/api.js'
import AuthLayout from '../components/auth/AuthLayout.jsx'
import AuthTextField from '../components/auth/AuthTextField.jsx'
import AuthSubmitButton from '../components/auth/AuthSubmitButton.jsx'
import AuthFeedback from '../components/auth/AuthFeedback.jsx'
import VerificationBadge from '../components/guide/VerificationBadge.jsx'
import GuideReviewsSection from '../components/reviews/GuideReviewsSection.jsx'
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

    const maxBio = profile?.role === 'Guide' ? 500 : 2000
    if (formData.bio && formData.bio.length > maxBio) {
      newErrors.bio = `Bio must be ${maxBio} characters or fewer.`
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
  const dashboardLink = isGuide ? '/guide/dashboard' : '/dashboard'

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
        { id: 'bio', label: 'Bio (Max 500 characters)', type: 'text', autoComplete: 'off', editable: true, value: formData.bio, multiline: true },
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

  const guideVerificationStatus = profile.guideProfile?.verificationStatus || 'Pending'
  const guideRating = profile.guideProfile?.averageRating

  return (
    <AuthLayout variant="signup">
      <div style={{ width: '100%', marginBottom: '16px' }}>
        <Link
          to={dashboardLink}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '13px',
            fontWeight: 600,
            color: 'var(--tm-teal-700)',
            textDecoration: 'none',
          }}
        >
          ← Return to Dashboard
        </Link>
      </div>

      <div className={styles.intro}>
        <h1 className={`${styles.title} ${styles.titleSignup}`}>Your Profile</h1>
        <p className={styles.subtitle}>Manage your account information and preferences.</p>
      </div>

      {/* Guide-specific Verification & Rating Banner */}
      {isGuide ? (
        <div
          style={{
            width: '100%',
            background: 'var(--tm-slate-50)',
            border: '1px solid var(--tm-border-soft)',
            borderRadius: '14px',
            padding: '18px 20px',
            marginBottom: '20px',
            display: 'grid',
            gridTemplateColumns: '1fr 1fr',
            gap: '16px',
          }}
        >
          <div>
            <span style={{ fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--tm-slate-500)', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
              Verification Status (Read-Only)
            </span>
            <VerificationBadge showDetail={false} status={guideVerificationStatus} />
          </div>

          <div>
            <span style={{ fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--tm-slate-500)', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
              Average Traveler Rating (Read-Only)
            </span>
            <span style={{ fontSize: '16px', fontWeight: 700, color: 'var(--tm-slate-800)' }}>
              {guideRating != null ? `${Number(guideRating).toFixed(1)} ★` : 'No ratings yet'}
            </span>
          </div>
        </div>
      ) : null}

      <div className={`${styles.authForm} ${styles.authFormSignup}`}>
        {feedback ? (
          <AuthFeedback detail={feedback.detail} message={feedback.message} tone={feedback.tone} />
        ) : null}

        <div className={`${styles.fields} ${styles.fieldsSignup}`}>
          {commonFields.map((field) => (
            <AuthTextField
              autoComplete={field.autoComplete}
              disabled={!field.editable || !isEditing}
              error={errors[field.id]}
              id={field.id}
              inputMode={field.type === 'tel' ? 'tel' : field.type === 'number' ? 'numeric' : undefined}
              key={field.id}
              label={field.label}
              onChange={(e) => handleChange(field.id, e.target.value)}
              type={field.type}
              value={field.value ?? ''}
              variant="signup"
            />
          ))}

          {roleFields.map((field) => (
            <AuthTextField
              autoComplete={field.autoComplete}
              disabled={!field.editable || !isEditing}
              error={errors[field.id]}
              id={field.id}
              inputMode={field.type === 'number' ? 'numeric' : undefined}
              key={field.id}
              label={field.label}
              multiline={field.multiline}
              onChange={(e) => handleChange(field.id, e.target.value)}
              type={field.type}
              value={field.value ?? ''}
              variant="signup"
            />
          ))}

          {!isEditing ? (
            <AuthSubmitButton onClick={() => setIsEditing(true)} variant="signup">
              Edit Profile
            </AuthSubmitButton>
          ) : (
            <div className={styles.actionsSignup}>
              <AuthSubmitButton loading={false} onClick={handleSave} variant="signup">
                Save Changes
              </AuthSubmitButton>
              <button
                className={`${styles.submitButton} ${styles.submitButtonSignup}`}
                onClick={handleCancel}
                type="button"
              >
                Cancel
              </button>
            </div>
          )}

          {profile?.role === 'Guide' && profile?.guideProfile?.id && (
            <div style={{ marginTop: 32, width: '100%' }}>
              <GuideReviewsSection
                guideProfileId={profile.guideProfile.id}
                title="Client Reviews & Ratings"
                token={token}
              />
            </div>
          )}
        </div>
      </div>
    </AuthLayout>
  )
}

export default ProfilePage
