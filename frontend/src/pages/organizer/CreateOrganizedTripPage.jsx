/**
 * TRAV-22 — Create an Organized Trip
 *
 * Presents the OrganizedTripForm with an empty draft. On success the backend
 * returns the saved trip (Draft status) and we redirect to its detail page.
 */

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import PageHeading from '../../components/tourist/PageHeading.jsx'
import OrganizedTripForm from '../../components/organizer/OrganizedTripForm.jsx'
import { useAuth } from '../../context/useAuth.js'
import { describeApiError, getFieldErrors } from '../../lib/api.js'
import {
  buildTripPayload,
  emptyTripDraft,
  validateOrganizedTripDraft,
} from '../../lib/organizerModel.js'
import { createOrganizedTrip } from '../../lib/organizerService.js'
import tStyles from '../../components/tourist/Tourist.module.css'

function CreateOrganizedTripPage() {
  const { user, token, logout } = useAuth()
  const navigate = useNavigate()

  const [draft, setDraft] = useState(emptyTripDraft)
  const [errors, setErrors] = useState({})
  const [serverError, setServerError] = useState(null)
  const [isSaving, setIsSaving] = useState(false)

  function handleChange(field, value) {
    setDraft((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev }
        delete next[field]
        return next
      })
    }
    if (serverError) setServerError(null)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    if (isSaving) return

    const nextErrors = validateOrganizedTripDraft(draft)
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors)
      // Focus first errored field
      const firstKey = Object.keys(nextErrors)[0]
      document.getElementById(`trip-${firstKey}`)?.focus()
      return
    }

    setIsSaving(true)
    setServerError(null)

    try {
      const trip = await createOrganizedTrip(token, buildTripPayload(draft))
      // Redirect to the new trip's detail page
      navigate(`/organizer/trips/${trip.id}`, { replace: true })
    } catch (err) {
      const fieldErrs = getFieldErrors(err)
      if (Object.keys(fieldErrs).length > 0) {
        setErrors(fieldErrs)
      }
      setServerError(describeApiError(err, 'The trip could not be created.'))
    } finally {
      setIsSaving(false)
    }
  }

  function handleCancel() {
    navigate('/organizer/dashboard')
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={tStyles.container}>
        <PageHeading
          backTo="/organizer/dashboard"
          backLabel="My trips"
          eyebrow="Organizer"
          title="Create an organized trip"
          description="Trips are saved as a Draft. Publish once you are ready to open guide applications."
        />

        <section className={tStyles.panel}>
          <OrganizedTripForm
            draft={draft}
            errors={errors}
            isSaving={isSaving}
            onChange={handleChange}
            onCancel={handleCancel}
            onSubmit={handleSubmit}
            serverError={serverError}
            submitLabel="Create trip"
          />
        </section>
      </div>
    </DashboardLayout>
  )
}

export default CreateOrganizedTripPage
