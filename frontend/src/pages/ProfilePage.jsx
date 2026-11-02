import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardLayout from '../components/dashboard/DashboardLayout.jsx'
import VerificationBadge from '../components/guide/VerificationBadge.jsx'
import GuideReviewsSection from '../components/reviews/GuideReviewsSection.jsx'
import { formatLongDate, getInitials } from '../components/dashboard/dashboardModel.js'
import { useAuth } from '../context/useAuth.js'
import { getProfile, updateProfile } from '../lib/profileService.js'
import { ApiError, describeApiError } from '../lib/api.js'
import styles from './Profile.module.css'

const LIMITS = {
  fullName: 100,
  phoneNumber: 20,
  preferences: 1000,
  guideBio: 500,
  organizerBio: 2000,
  serviceArea: 100,
  supportedTripTypes: 500,
  photoUrl: 500,
  groupOrganizationName: 253,
}

const DRAFT_FIELDS = [
  'fullName',
  'phoneNumber',
  'preferences',
  'bio',
  'serviceArea',
  'experienceYears',
  'supportedTripTypes',
  'photoUrl',
  'groupOrganizationName',
]

const ACCOUNT_FIELDS = [
  {
    id: 'fullName',
    label: 'Full name',
    required: true,
    max: LIMITS.fullName,
    autoComplete: 'name',
    placeholder: 'e.g. Ayesha Rahman',
    helper: 'Shown wherever your TravelMatch profile appears.',
  },
  {
    id: 'phoneNumber',
    label: 'Phone number',
    max: LIMITS.phoneNumber,
    type: 'tel',
    inputMode: 'tel',
    autoComplete: 'tel',
    placeholder: 'e.g. +880 1712 345 678',
    helper: 'Shared only with matches you accept.',
  },
]

function draftFrom(profile) {
  return {
    fullName: profile?.fullName ?? '',
    phoneNumber: profile?.phoneNumber ?? '',
    preferences: profile?.touristProfile?.preferences ?? '',
    bio: profile?.guideProfile?.bio ?? profile?.organizerProfile?.bio ?? '',
    serviceArea: profile?.guideProfile?.serviceArea ?? '',
    experienceYears:
      profile?.guideProfile?.experienceYears === null ||
      profile?.guideProfile?.experienceYears === undefined
        ? ''
        : String(profile.guideProfile.experienceYears),
    supportedTripTypes: profile?.guideProfile?.supportedTripTypes ?? '',
    photoUrl: profile?.guideProfile?.photoUrl ?? '',
    groupOrganizationName: profile?.organizerProfile?.groupOrganizationName ?? '',
  }
}

function roleFieldDefs(role) {
  if (role === 'Tourist') {
    return [
      {
        id: 'preferences',
        label: 'Travel preferences',
        full: true,
        multiline: true,
        max: LIMITS.preferences,
        placeholder: 'e.g. relaxed pace, vegetarian food, hill trekking, prefers Bangla and English…',
        helper: 'Guides read this when they tailor a proposal for you.',
      },
    ]
  }

  if (role === 'Guide') {
    return [
      {
        id: 'bio',
        label: 'Professional bio',
        full: true,
        multiline: true,
        max: LIMITS.guideBio,
        placeholder: 'Introduce yourself: the regions you know, languages you speak, and the kind of trips you run best.',
        helper: 'Travelers see this before they accept your proposal.',
      },
      {
        id: 'serviceArea',
        label: 'Service area',
        max: LIMITS.serviceArea,
        placeholder: 'e.g. Rangamati, Sajek Valley, Chittagong Hill Tracts',
      },
      {
        id: 'experienceYears',
        label: 'Years of experience',
        type: 'number',
        inputMode: 'numeric',
        placeholder: 'e.g. 6',
        helper: 'Leave blank to keep the current value.',
      },
      {
        id: 'supportedTripTypes',
        label: 'Supported trip types',
        full: true,
        max: LIMITS.supportedTripTypes,
        placeholder: 'e.g. Nature & Hiking, Cultural Tours, Family Trips',
        helper: 'Separate the trip types you cover with commas.',
      },
      {
        id: 'photoUrl',
        label: 'Profile photo URL',
        full: true,
        type: 'url',
        max: LIMITS.photoUrl,
        placeholder: 'https://…',
        helper: 'A direct link to a square or portrait image.',
        preview: true,
      },
    ]
  }

  if (role === 'Organizer') {
    return [
      {
        id: 'groupOrganizationName',
        label: 'Group or organization name',
        max: LIMITS.groupOrganizationName,
        autoComplete: 'organization',
        placeholder: 'e.g. Hill Trails Tours',
      },
      {
        id: 'bio',
        label: 'About the organization',
        full: true,
        multiline: true,
        max: LIMITS.organizerBio,
        placeholder: 'Describe the trips your organization runs and what makes them distinctive.',
      },
    ]
  }

  return []
}

function toChips(value) {
  return String(value ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean)
}

function validateDraft(draft, role) {
  const errors = {}

  if (!draft.fullName?.trim()) {
    errors.fullName = 'Enter your full name.'
  }

  if (role === 'Guide' && draft.experienceYears !== '') {
    const years = Number(draft.experienceYears)
    if (!Number.isFinite(years) || years < 0 || !Number.isInteger(years)) {
      errors.experienceYears = 'Use a whole number of years, such as 3.'
    }
  }

  return errors
}

function buildPayload(draft, profile, role) {
  const payload = {}

  const name = draft.fullName.trim()
  if (name && name !== (profile.fullName ?? '')) payload.fullName = name

  const phone = draft.phoneNumber.trim()
  if (phone && phone !== (profile.phoneNumber ?? '')) payload.phoneNumber = phone

  if (role === 'Tourist') {
    if (draft.preferences !== (profile.touristProfile?.preferences ?? '')) {
      payload.preferences = draft.preferences
    }
  }

  if (role === 'Guide') {
    const guide = profile.guideProfile ?? {}
    if (draft.bio !== (guide.bio ?? '')) payload.bio = draft.bio
    if (draft.serviceArea !== (guide.serviceArea ?? '')) payload.serviceArea = draft.serviceArea
    if (draft.supportedTripTypes !== (guide.supportedTripTypes ?? '')) {
      payload.supportedTripTypes = draft.supportedTripTypes
    }
    if (draft.photoUrl !== (guide.photoUrl ?? '')) payload.photoUrl = draft.photoUrl

    const years = draft.experienceYears === '' ? null : Number(draft.experienceYears)
    const currentYears = guide.experienceYears ?? null
    if (years !== null && years !== currentYears) payload.experienceYears = years
  }

  if (role === 'Organizer') {
    const organizer = profile.organizerProfile ?? {}
    if (draft.groupOrganizationName !== (organizer.groupOrganizationName ?? '')) {
      payload.groupOrganizationName = draft.groupOrganizationName
    }
    if (draft.bio !== (organizer.bio ?? '')) payload.bio = draft.bio
  }

  return payload
}

function ProfilePage() {
  const { user, token, isAuthenticated, isLoading: authLoading, logout } = useAuth()
  const [profile, setProfile] = useState(null)
  const [draft, setDraft] = useState(() => draftFrom(null))
  const [errors, setErrors] = useState({})
  const [feedback, setFeedback] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isEditing, setIsEditing] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [brokenPhotoUrl, setBrokenPhotoUrl] = useState(null)
  const formRef = useRef(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      if (!isAuthenticated || !token) {
        setIsLoading(false)
        return
      }

      try {
        const data = await getProfile(token)
        if (cancelled) return
        setProfile(data)
        setDraft(draftFrom(data))
      } catch (error) {
        if (cancelled) return
        setFeedback({
          tone: 'error',
          message: 'We could not load your profile',
          detail:
            error instanceof ApiError
              ? describeApiError(error, 'Please refresh the page and try again.')
              : 'Check your connection, then refresh the page.',
        })
      } finally {
        if (!cancelled) setIsLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, token])

  const role = profile?.role
  const roleFields = useMemo(() => roleFieldDefs(role), [role])

  const dirty = useMemo(() => {
    const baseline = draftFrom(profile)
    return DRAFT_FIELDS.some((field) => draft[field] !== baseline[field])
  }, [draft, profile])

  function startEditing() {
    setFeedback(null)
    setIsEditing(true)
    requestAnimationFrame(() => {
      formRef.current?.querySelector('input:not([readonly]), textarea')?.focus()
    })
  }

  const stopEditing = useCallback(() => {
    setDraft(draftFrom(profile))
    setErrors({})
    setIsEditing(false)
  }, [profile])

  useEffect(() => {
    if (!isEditing) return undefined

    function handleKeyDown(event) {
      if (event.key === 'Escape' && !isSaving) stopEditing()
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isEditing, isSaving, stopEditing])

  useEffect(() => {
    if (feedback?.tone !== 'success') return undefined

    const timer = setTimeout(() => setFeedback(null), 5000)
    return () => clearTimeout(timer)
  }, [feedback])

  function handleChange(field, value) {
    setDraft((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev }
        delete next[field]
        return next
      })
    }
  }

  async function handleSave(event) {
    event.preventDefault()
    if (isSaving) return

    const nextErrors = validateDraft(draft, role)
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors)
      const firstError = Object.keys(nextErrors)[0]
      requestAnimationFrame(() => document.getElementById(firstError)?.focus())
      return
    }

    const payload = buildPayload(draft, profile, role)
    if (Object.keys(payload).length === 0) {
      setFeedback({ tone: 'info', message: 'Nothing to save yet', detail: 'No fields were changed.' })
      setIsEditing(false)
      return
    }

    setIsSaving(true)
    setFeedback(null)

    try {
      const updated = await updateProfile(token, payload)
      setProfile(updated)
      setDraft(draftFrom(updated))
      setErrors({})
      setIsEditing(false)
      setFeedback({ tone: 'success', message: 'Profile updated', detail: 'Your changes are saved.' })
    } catch (error) {
      setFeedback({
        tone: 'error',
        message: 'We could not save your changes',
        detail:
          error instanceof ApiError
            ? describeApiError(error, 'Please check the highlighted fields and try again.')
            : 'Your connection dropped. Your edits are still here, so you can try again.',
      })
    } finally {
      setIsSaving(false)
    }
  }

  if (authLoading || isLoading) {
    return (
      <DashboardLayout onLogout={logout} user={user}>
        <div className={styles.loadingWrap}>
          <span className={styles.loadingSpinner} aria-hidden="true" />
          <p>Loading your profile...</p>
        </div>
      </DashboardLayout>
    )
  }

  if (!isAuthenticated || !profile) {
    return (
      <DashboardLayout onLogout={logout} user={user}>
        <div className={styles.emptyCard}>
          <h1 className={styles.emptyTitle}>Profile unavailable</h1>
          <p className={styles.emptyText}>
            {feedback?.detail ?? 'Sign in to see and manage your profile details.'}
          </p>
          <Link className={styles.primaryBtn} to="/login">
            Back to sign in
          </Link>
        </div>
      </DashboardLayout>
    )
  }

  const isGuide = role === 'Guide'
  const guideStatus = profile.guideProfile?.verificationStatus ?? 'Pending'
  const guideRating = profile.guideProfile?.averageRating
  const dashboardLink = isGuide ? '/guide/dashboard' : '/dashboard'
  const rolePillClass =
    role === 'Guide' ? styles.rolePillGuide : role === 'Organizer' ? styles.rolePillOrganizer : ''
  const savedPhotoUrl = profile.guideProfile?.photoUrl ?? ''
  const draftPhotoUrl = draft.photoUrl?.trim() ?? ''
  const candidatePhotoUrl = isEditing && draftPhotoUrl ? draftPhotoUrl : savedPhotoUrl
  const avatarSource =
    candidatePhotoUrl && candidatePhotoUrl !== brokenPhotoUrl ? candidatePhotoUrl : ''

  function renderReadOnly(field) {
    const value = draft[field.id]

    if (field.preview) {
      return (
        <div className={`${styles.field} ${styles.fieldFull}`} key={field.id}>
          <span className={styles.label}>{field.label}</span>
          <div className={styles.photoPreview}>
            {value ? (
              <img
                alt="Profile photo"
                className={styles.photoThumbLarge}
                onError={() => setBrokenPhotoUrl(value)}
                src={avatarSource}
              />
            ) : (
              <span aria-hidden="true" className={styles.photoThumbLarge} />
            )}
            <p className={styles.photoNote}>
              {value
                ? 'This image is used as your profile photo across TravelMatch.'
                : 'No photo linked yet. Add one so travelers can put a face to your profile.'}
            </p>
          </div>
        </div>
      )
    }

    return (
      <div className={`${styles.field} ${field.full ? styles.fieldFull : ''}`} key={field.id}>
        <span className={styles.label}>{field.label}</span>

        {field.id === 'supportedTripTypes' && toChips(value).length > 0 ? (
          <ul className={styles.chips}>
            {toChips(value).map((chip) => (
              <li className={styles.chip} key={chip}>
                {chip}
              </li>
            ))}
          </ul>
        ) : (
          <p className={`${styles.readValue} ${value ? '' : styles.readEmpty}`}>
            {value || 'Not added yet'}
          </p>
        )}

        {field.helper ? <p className={styles.helper}>{field.helper}</p> : null}
      </div>
    )
  }

  function renderEditable(field) {
    const value = draft[field.id] ?? ''
    const error = errors[field.id]
    const describedBy = [error ? `${field.id}-error` : null, field.helper ? `${field.id}-helper` : null]
      .filter(Boolean)
      .join(' ') || undefined

    return (
      <div className={`${styles.field} ${field.full ? styles.fieldFull : ''}`} key={field.id}>
        <label className={styles.label} htmlFor={field.id}>
          {field.label}
          {field.required ? (
            <span className={styles.requiredMark} title="Required">
              *
            </span>
          ) : (
            <span className={styles.optionalMark}>Optional</span>
          )}
        </label>

        {field.multiline ? (
          <textarea
            aria-describedby={describedBy}
            aria-invalid={error ? 'true' : undefined}
            className={`${styles.textarea} ${error ? styles.inputError : ''}`}
            id={field.id}
            maxLength={field.max}
            onChange={(event) => handleChange(field.id, event.target.value)}
            placeholder={field.placeholder}
            rows={field.id === 'bio' ? 5 : 4}
            value={value}
          />
        ) : (
          <input
            autoComplete={field.autoComplete}
            aria-describedby={describedBy}
            aria-invalid={error ? 'true' : undefined}
            className={`${styles.input} ${error ? styles.inputError : ''}`}
            id={field.id}
            inputMode={field.inputMode}
            max={field.type === 'number' ? 60 : undefined}
            min={field.type === 'number' ? 0 : undefined}
            maxLength={field.max}
            onChange={(event) => handleChange(field.id, event.target.value)}
            placeholder={field.placeholder}
            step={field.type === 'number' ? '1' : undefined}
            type={field.type ?? 'text'}
            value={value}
          />
        )}

        {error ? (
          <p className={styles.fieldError} id={`${field.id}-error`} role="alert">
            {error}
          </p>
        ) : null}

        <div className={styles.counterRow}>
          {field.helper ? (
            <p className={styles.helper} id={`${field.id}-helper`}>
              {field.helper}
            </p>
          ) : (
            <span />
          )}
          {field.max ? (
            <p
              className={`${styles.charCount} ${
                value.length >= field.max
                  ? styles.charCountMax
                  : value.length > field.max * 0.85
                    ? styles.charCountWarn
                    : ''
              }`}
            >
              {value.length} / {field.max}
            </p>
          ) : null}
        </div>

        {field.preview ? (
          <div className={styles.photoPreview}>
            {avatarSource ? (
              <img
                alt=""
                className={styles.photoThumb}
                onError={() => setBrokenPhotoUrl(candidatePhotoUrl)}
                src={avatarSource}
              />
            ) : (
              <span aria-hidden="true" className={styles.photoThumb} />
            )}
            <p className={styles.photoNote}>
              {avatarSource
                ? 'This image is used as your profile photo across TravelMatch.'
                : candidatePhotoUrl
                  ? 'This image link could not be loaded — check the address.'
                  : 'Add a photo link to personalize your guide profile.'}
            </p>
          </div>
        ) : null}
      </div>
    )
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.topBar}>
          <Link className={styles.backLink} to={dashboardLink}>
            <span aria-hidden="true">←</span>
            {isGuide ? 'Guide dashboard' : 'Dashboard'}
          </Link>
        </div>

        <header className={styles.pageHeader}>
          <div>
            <h1 className={styles.pageTitle}>Profile settings</h1>
            <p className={styles.pageSubtitle}>
              Keep your contact details and {isGuide ? 'guide' : role === 'Organizer' ? 'organization' : 'travel'}{' '}
              information up to date so matches and proposals reach you.
            </p>
          </div>
          {!isEditing ? (
            <button className={styles.primaryBtn} onClick={startEditing} type="button">
              Edit profile
            </button>
          ) : null}
        </header>

        {feedback ? (
          <div
            aria-live={feedback.tone === 'error' ? 'assertive' : 'polite'}
            className={`${styles.banner} ${
              feedback.tone === 'success'
                ? styles.bannerSuccess
                : feedback.tone === 'error'
                  ? styles.bannerError
                  : ''
            }`}
            role={feedback.tone === 'error' ? 'alert' : 'status'}
          >
            <span aria-hidden="true" className={styles.bannerMarker}>
              {feedback.tone === 'success' ? '✓' : '!'}
            </span>
            <span className={styles.bannerBody}>
              <strong className={styles.bannerMessage}>{feedback.message}</strong>
              {feedback.detail ? (
                <span className={styles.bannerDetail}>{feedback.detail}</span>
              ) : null}
            </span>
            <button
              aria-label="Dismiss message"
              className={styles.bannerClose}
              onClick={() => setFeedback(null)}
              type="button"
            >
              ×
            </button>
          </div>
        ) : null}

        <div className={styles.layout}>
          <aside aria-label="Account summary" className={styles.identityCard}>
            <div className={styles.identityHead}>
              <span aria-hidden="true" className={styles.avatar}>
                {avatarSource ? (
                  <img
                    alt=""
                    className={styles.avatarImage}
                    onError={() => setBrokenPhotoUrl(candidatePhotoUrl)}
                    src={avatarSource}
                  />
                ) : (
                  getInitials(profile.fullName)
                )}
              </span>
              <p className={styles.identityName}>{profile.fullName}</p>
              <span className={`${styles.rolePill} ${rolePillClass}`}>{role}</span>
            </div>

            <dl className={styles.metaList}>
              <div className={styles.metaRow}>
                <dt className={styles.metaLabel}>Email</dt>
                <dd className={styles.metaValue}>{profile.email}</dd>
              </div>
              <div className={styles.metaRow}>
                <dt className={styles.metaLabel}>Member since</dt>
                <dd className={styles.metaValue}>{formatLongDate(profile.createdAt) ?? '—'}</dd>
              </div>
              <div className={styles.metaRow}>
                <dt className={styles.metaLabel}>Account</dt>
                <dd className={`${styles.metaValue} ${profile.isActive === false ? styles.statusInactive : styles.statusActive}`}>
                  <span aria-hidden="true" className={styles.statusDot} />
                  {profile.isActive === false ? 'Inactive' : 'Active'}
                </dd>
              </div>
            </dl>

            {isGuide ? (
              <div className={styles.guideStats}>
                <div className={styles.guideStat}>
                  <span className={styles.metaLabel}>Verification</span>
                  <VerificationBadge showDetail={false} status={guideStatus} />
                </div>
                <div className={styles.guideStat}>
                  <span className={styles.metaLabel}>Rating</span>
                  <span className={styles.guideStatValue}>
                    {guideRating != null ? `${Number(guideRating).toFixed(1)} ★` : 'No ratings'}
                  </span>
                </div>
              </div>
            ) : null}
          </aside>

          <form className={styles.sections} noValidate onSubmit={handleSave} ref={formRef}>
            <section aria-labelledby="account-section-title" className={styles.card}>
              <header className={styles.cardHeader}>
                <div>
                  <h2 className={styles.cardTitle} id="account-section-title">
                    Account
                  </h2>
                  <p className={styles.cardHint}>
                    Your sign-in details and how other travelers contact you.
                  </p>
                </div>
                <span className={styles.cardBadge}>Required</span>
              </header>

              <div className={styles.fieldGrid}>
                {isEditing
                  ? ACCOUNT_FIELDS.map(renderEditable)
                  : ACCOUNT_FIELDS.map(renderReadOnly)}

                <div className={`${styles.field} ${styles.fieldFull}`}>
                  <span className={styles.label}>Email address</span>
                  {isEditing ? (
                    <div className={styles.staticShell}>
                      <span className={styles.staticValue}>{profile.email}</span>
                      <span className={styles.lockMark}>Contact support to change</span>
                    </div>
                  ) : (
                    <p className={styles.readValue}>{profile.email}</p>
                  )}
                </div>
              </div>
            </section>

            {roleFields.length > 0 ? (
              <section aria-labelledby="role-section-title" className={styles.card}>
                <header className={styles.cardHeader}>
                  <div>
                    <h2 className={styles.cardTitle} id="role-section-title">
                      {role} details
                    </h2>
                    <p className={styles.cardHint}>
                      {role === 'Guide'
                        ? 'This is what travelers compare when they pick a guide.'
                        : role === 'Organizer'
                          ? 'Shown on the trips your organization publishes.'
                          : 'Helps guides shape a proposal that fits how you like to travel.'}
                    </p>
                  </div>
                </header>

                <div className={styles.fieldGrid}>
                  {isEditing ? roleFields.map(renderEditable) : roleFields.map(renderReadOnly)}
                </div>
              </section>
            ) : (
              <section aria-labelledby="role-section-title" className={styles.card}>
                <header className={styles.cardHeader}>
                  <div>
                    <h2 className={styles.cardTitle} id="role-section-title">
                      Administrator account
                    </h2>
                    <p className={styles.cardHint}>
                      Admin accounts manage the platform, so there are no traveler or guide details to edit here.
                    </p>
                  </div>
                </header>
              </section>
            )}

            {isEditing ? (
              <div className={styles.actionBar}>
                <div>
                  {dirty ? (
                    <p className={styles.actionStateStrong}>
                      <span aria-hidden="true" className={styles.dirtyDot} />
                      You have unsaved changes
                    </p>
                  ) : (
                    <p className={styles.actionState}>Edit a field to save changes.</p>
                  )}
                  <p className={styles.actionState}>Press Esc to discard.</p>
                </div>
                <div className={styles.actionButtons}>
                  <button className={styles.secondaryBtn} disabled={isSaving} onClick={stopEditing} type="button">
                    Cancel
                  </button>
                  <button className={styles.primaryBtn} disabled={!dirty || isSaving} type="submit">
                    {isSaving ? (
                      <>
                        <span className={styles.spinner} aria-hidden="true" />
                        Saving...
                      </>
                    ) : (
                      'Save changes'
                    )}
                  </button>
                </div>
              </div>
            ) : null}
          </form>
        </div>

        {isGuide && profile.guideProfile?.id ? (
          <div className={styles.reviewsCard}>
            <GuideReviewsSection guideProfileId={profile.guideProfile.id} token={token} title="Client reviews & ratings" />
          </div>
        ) : null}
      </div>
    </DashboardLayout>
  )
}

export default ProfilePage
