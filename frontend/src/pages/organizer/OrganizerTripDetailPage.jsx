import GuideApplicationsPanel from '../../components/organizer/GuideApplicationsPanel.jsx'
/**
 * TRAV-23 — Manage My Organized Trips (detail view)
 *
 * Shows full trip details for one of the organizer's own trips.
 * Supported actions (gated by backend business rules):
 *   • Edit   — inline form, allowed for Draft / ApplicationOpen / GuideSelection
 *   • Publish — Draft → ApplicationOpen
 *   • Cancel  — blocked for InProgress / Completed / Cancelled
 *   • Complete — RegistrationOpen / RegistrationClosed / InProgress (+ end date past)
 */

import { useCallback, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import StateNotice from '../../components/dashboard/StateNotice.jsx'
import ConfirmDialog from '../../components/tourist/ConfirmDialog.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import PageHeading from '../../components/tourist/PageHeading.jsx'
import StatusPill from '../../components/tourist/StatusPill.jsx'
import OrganizedTripForm from '../../components/organizer/OrganizedTripForm.jsx'
import {
  formatDateRange,
  formatLongDate,
} from '../../components/dashboard/dashboardModel.js'
import { formatDateLabel } from '../../components/tourist/touristModel.js'
import {
  buildTripPayload,
  canCancelTrip,
  canCompleteTrip,
  canEditTrip,
  canPublishTrip,
  draftFromTrip,
  getOrganizerTripStatusMeta,
  isTripTerminal,
  validateOrganizedTripDraft,
} from '../../lib/organizerModel.js'
import {
  cancelOrganizedTrip,
  completeOrganizedTrip,
  publishOrganizedTrip,
  updateOrganizedTrip,
} from '../../lib/organizerService.js'
import { useAuth } from '../../context/useAuth.js'
import { describeApiError, getFieldErrors } from '../../lib/api.js'
import { ERROR, LOADING, READY, useResource } from '../../lib/useResource.js'
import { getMyOrganizedTrips } from '../../lib/organizerService.js'
import tStyles from '../../components/tourist/Tourist.module.css'
import oStyles from '../../components/organizer/Organizer.module.css'

// ── Confirm-dialog copy ───────────────────────────────────────────────────────

const ACTION_COPY = {
  publish: {
    title: 'Publish this trip?',
    confirmLabel: 'Publish',
    description:
      'Publishing opens the trip for guide applications (status → Application Open). ' +
      'You can still edit dates, price and details while applications are open.',
  },
  cancel: {
    title: 'Cancel this trip?',
    confirmLabel: 'Cancel trip',
    description:
      'Cancelling is permanent. Tourists with active registrations will not be ' +
      'automatically notified by the platform — you may want to contact them separately.',
  },
  complete: {
    title: 'Mark this trip as completed?',
    confirmLabel: 'Mark completed',
    description:
      'Completion enables tourists to release their escrow deposits. ' +
      'The backend requires the trip end date to have already passed.',
  },
}

// ── Detail section ────────────────────────────────────────────────────────────

function TripDetailSection({ trip }) {
  const statusMeta = getOrganizerTripStatusMeta(trip.status)
  const deadline = formatDateLabel(trip.registrationDeadline)

  return (
    <section className={tStyles.card} aria-label="Trip details">
      <div className={tStyles.cardHead}>
        <div>
          <h2 className={tStyles.cardTitle}>
            {trip.title?.trim() || `Trip #${trip.id}`}
          </h2>
          <p className={tStyles.cardSubtitle}>
            {trip.destination?.trim() || 'Destination not set'}
          </p>
        </div>
        <StatusPill label={statusMeta.label} tone={statusMeta.tone} />
      </div>

      <dl className={tStyles.metaGrid}>
        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Travel dates</dt>
          <dd className={tStyles.metaValue}>{formatDateRange(trip.startDate, trip.endDate)}</dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Registration deadline</dt>
          <dd className={tStyles.metaValue}>{deadline ?? 'Not set'}</dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Max participants</dt>
          <dd className={tStyles.metaValue}>{trip.maxParticipants ?? '—'}</dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Price per person</dt>
          <dd className={tStyles.metaValue}>
            {trip.pricePerPerson != null
              ? Number(trip.pricePerPerson).toLocaleString('en-US', { maximumFractionDigits: 2 })
              : '—'}
            <span className={tStyles.metaNote}>No currency stored</span>
          </dd>
        </div>

        <div className={tStyles.metaItem}>
          <dt className={tStyles.metaLabel}>Created</dt>
          <dd className={tStyles.metaValue}>{formatLongDate(trip.createdAt) ?? '—'}</dd>
        </div>

        {trip.updatedAt ? (
          <div className={tStyles.metaItem}>
            <dt className={tStyles.metaLabel}>Last updated</dt>
            <dd className={tStyles.metaValue}>{formatLongDate(trip.updatedAt)}</dd>
          </div>
        ) : null}
      </dl>

      {trip.description?.trim() ? (
        <>
          <h3 className={tStyles.sectionTitle} style={{ marginTop: 20 }}>Description</h3>
          <p className={tStyles.prose}>{trip.description}</p>
        </>
      ) : null}

      {trip.inclusions?.trim() ? (
        <>
          <h3 className={tStyles.sectionTitle} style={{ marginTop: 16 }}>Inclusions</h3>
          <p className={tStyles.prose}>{trip.inclusions}</p>
        </>
      ) : null}

      {trip.exclusions?.trim() ? (
        <>
          <h3 className={tStyles.sectionTitle} style={{ marginTop: 16 }}>Exclusions</h3>
          <p className={tStyles.prose}>{trip.exclusions}</p>
        </>
      ) : null}
    </section>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

function OrganizerTripDetailPage() {
  const { tripId } = useParams()
  const routeId = Number(tripId)
  const { user, token, logout } = useAuth()
  const location = useLocation()

  // UI state
  const [isEditing, setIsEditing] = useState(false)
  const [editDraft, setEditDraft] = useState(null)
  const [editErrors, setEditErrors] = useState({})
  const [editServerError, setEditServerError] = useState(null)
  const [isSaving, setIsSaving] = useState(false)

  const [pendingAction, setPendingAction] = useState(
    // Dashboard "Publish" quick-link passes state={{ promptPublish: true }}
    location.state?.promptPublish ? 'publish' : null,
  )
  const [actionError, setActionError] = useState(null)
  const [isActioning, setIsActioning] = useState(false)
  const [feedback, setFeedback] = useState(null)

  // Load the organizer's own trips and find this one by id.
  // Using getMyOrganizedTrips rather than an imaginary getMyOrganizedTrip(id)
  // because the controller only exposes a list endpoint (GET /my), not a
  // single-item endpoint, for organizer-owned trips.
  const loader = useCallback(async () => {
    if (!Number.isInteger(routeId) || routeId <= 0) {
      throw new Error('Invalid trip link.')
    }
    const trips = await getMyOrganizedTrips(token)
    const found = trips.find((t) => t.id === routeId)
    if (!found) throw new Error('Trip not found or you do not own it.')
    return found
  }, [routeId, token])

  const { status, data: trip, error, reload } = useResource(loader, logout)

  // ── Edit helpers ────────────────────────────────────────────────────────────

  function startEdit() {
    if (!trip) return
    setEditDraft(draftFromTrip(trip))
    setEditErrors({})
    setEditServerError(null)
    setIsEditing(true)
    setFeedback(null)
  }

  function cancelEdit() {
    setIsEditing(false)
    setEditDraft(null)
    setEditErrors({})
    setEditServerError(null)
  }

  function handleEditChange(field, value) {
    setEditDraft((prev) => ({ ...prev, [field]: value }))
    if (editErrors[field]) {
      setEditErrors((prev) => {
        const next = { ...prev }
        delete next[field]
        return next
      })
    }
    if (editServerError) setEditServerError(null)
  }

  async function handleEditSubmit(event) {
    event.preventDefault()
    if (isSaving || !editDraft) return

    // Skip start-date-in-past check when the saved date already exists
    const nextErrors = validateOrganizedTripDraft(editDraft, { skipDateInPast: true })
    if (Object.keys(nextErrors).length > 0) {
      setEditErrors(nextErrors)
      const firstKey = Object.keys(nextErrors)[0]
      document.getElementById(`trip-${firstKey}`)?.focus()
      return
    }

    setIsSaving(true)
    setEditServerError(null)

    try {
      await updateOrganizedTrip(token, routeId, buildTripPayload(editDraft))
      setIsEditing(false)
      setEditDraft(null)
      setFeedback({ tone: 'success', message: 'Trip updated successfully.' })
      reload()
    } catch (err) {
      const fieldErrs = getFieldErrors(err)
      if (Object.keys(fieldErrs).length > 0) setEditErrors(fieldErrs)
      setEditServerError(describeApiError(err, 'The trip could not be updated.'))
    } finally {
      setIsSaving(false)
    }
  }

  // ── Lifecycle action helpers ────────────────────────────────────────────────

  function openAction(action) {
    setActionError(null)
    setPendingAction(action)
    setFeedback(null)
  }

  function closeDialog() {
    if (isActioning) return
    setPendingAction(null)
    setActionError(null)
  }

  async function handleActionConfirm() {
    if (!pendingAction || isActioning) return
    setIsActioning(true)
    setActionError(null)

    try {
      if (pendingAction === 'publish') {
        await publishOrganizedTrip(token, routeId)
        setFeedback({ tone: 'success', message: 'Trip published. It is now open for guide applications.' })
      } else if (pendingAction === 'cancel') {
        await cancelOrganizedTrip(token, routeId)
        setFeedback({ tone: 'success', message: 'Trip cancelled.' })
      } else if (pendingAction === 'complete') {
        await completeOrganizedTrip(token, routeId)
        setFeedback({ tone: 'success', message: 'Trip marked as completed. Tourists can now release their escrow.' })
      }
      setPendingAction(null)
      reload()
    } catch (err) {
      setActionError(describeApiError(err, 'The action could not be completed.'))
    } finally {
      setIsActioning(false)
    }
  }

  // ── Derived action availability (from loaded trip) ──────────────────────────

  const editable = trip ? canEditTrip(trip.status) : false
  const publishable = trip ? canPublishTrip(trip.status) : false
  const cancellable = trip ? canCancelTrip(trip.status) : false
  const completable = trip ? canCompleteTrip(trip.status) : false
  const terminal = trip ? isTripTerminal(trip.status) : false

  const actionCopy = pendingAction ? ACTION_COPY[pendingAction] : null

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={tStyles.container}>
        <PageHeading
          backTo="/organizer/dashboard"
          backLabel="My trips"
          eyebrow="Organizer"
          title={trip?.title?.trim() || 'Trip details'}
          description={trip?.destination?.trim() || undefined}
        >
          <Link className={tStyles.secondaryButton} to={`/organized-trips/${routeId}/chat`}>Group chat</Link>
          {!isEditing ? (
            <Link
              className={tStyles.secondaryButton}
              to={`/organizer/trips/${routeId}/registrations`}
            >
              Registered tourists
            </Link>
          ) : null}
        </PageHeading>

        {feedback ? (
          <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner>
        ) : null}

        {/* ── Loading ── */}
        {status === LOADING ? (
          <div className={tStyles.skeletonPanel} aria-hidden="true" />
        ) : null}

        {/* ── Error ── */}
        {status === ERROR ? (
          <StateNotice
            actionLabel="Retry"
            detail={describeApiError(error, 'This trip could not be loaded.')}
            onAction={reload}
            title="Couldn't load trip"
            tone="error"
          >
            <Link className={tStyles.secondaryButton} to="/organizer/dashboard">
              Back to my trips
            </Link>
          </StateNotice>
        ) : null}

        {/* ── Ready ── */}
        {status === READY && trip ? (
          <>
            <TripDetailSection trip={trip} />
            <FeedbackBanner>
              {trip.status === 'Draft' ? 'Next: publish this draft so guides can find it and apply. Tourists will see it after you select a guide.'
                : ['ApplicationOpen', 'GuideSelection'].includes(trip.status) ? 'Next: review guide applications below and select a guide to open tourist registration.'
                  : trip.status === 'RegistrationOpen' ? 'A guide has been selected. Tourists can find this trip while the registration deadline has not passed and spots remain.'
                    : 'Registration is not open for this trip.'}
            </FeedbackBanner>
            {trip.status !== 'Draft' && !isEditing && <GuideApplicationsPanel key={trip.id} trip={trip} token={token} logout={logout} onSelected={() => {
              setFeedback({ tone: 'success', message: 'Guide selected. Tourist registration is now open.' })
              reload()
            }} />}

            {/* ── Action buttons ── */}
            {!isEditing ? (
              <div className={oStyles.actionRow}>
                {editable ? (
                  <button
                    className={tStyles.secondaryButton}
                    onClick={startEdit}
                    type="button"
                  >
                    Edit trip
                  </button>
                ) : null}

                {publishable ? (
                  <button
                    className={tStyles.primaryButton}
                    onClick={() => openAction('publish')}
                    type="button"
                  >
                    Publish trip
                  </button>
                ) : null}

                {completable ? (
                  <button
                    className={tStyles.secondaryButton}
                    onClick={() => openAction('complete')}
                    type="button"
                  >
                    Mark as completed
                  </button>
                ) : null}

                {!editable && !publishable && !completable && !cancellable && !terminal ? (
                  <span className={tStyles.hintText}>
                    No actions are available for this trip in its current status.
                  </span>
                ) : null}
              </div>
            ) : null}

            {/* ── Inline edit form ── */}
            {isEditing && editDraft ? (
              <section className={tStyles.panel} aria-label="Edit trip">
                <div className={tStyles.panelHead}>
                  <h2 className={tStyles.sectionTitle}>Edit trip</h2>
                </div>
                <p className={tStyles.sectionLead}>
                  Editing is allowed while the trip is in{' '}
                  <strong>Draft</strong>, <strong>Application Open</strong> or{' '}
                  <strong>Guide Selection</strong> status. Current status:{' '}
                  <strong>{trip.status}</strong>.
                </p>
                <OrganizedTripForm
                  draft={editDraft}
                  errors={editErrors}
                  isEdit
                  isSaving={isSaving}
                  onChange={handleEditChange}
                  onCancel={cancelEdit}
                  onSubmit={handleEditSubmit}
                  serverError={editServerError}
                  submitLabel="Save changes"
                />
              </section>
            ) : null}

            {/* ── Cancel danger zone (always shown unless terminal) ── */}
            {cancellable && !isEditing ? (
              <div className={oStyles.dangerZone}>
                <div className={oStyles.dangerText}>
                  <p className={oStyles.dangerTitle}>Cancel this trip</p>
                  <p className={oStyles.dangerLead}>
                    Cancelling is permanent. Trips that are in progress or already completed
                    cannot be cancelled.
                  </p>
                </div>
                <button
                  className={tStyles.dangerButton}
                  onClick={() => openAction('cancel')}
                  type="button"
                >
                  Cancel trip
                </button>
              </div>
            ) : null}

            {/* ── Completed / Cancelled terminal notice ── */}
            {terminal && !isEditing ? (
              <div className={oStyles.infoNotice}>
                <strong>
                  {trip.status === 'Completed' ? 'Trip completed.' : 'Trip cancelled.'}
                </strong>{' '}
                {trip.status === 'Completed'
                  ? 'Tourists can now release their escrow deposits from the Payments page.'
                  : 'No further actions are available for a cancelled trip.'}
              </div>
            ) : null}
          </>
        ) : null}
      </div>

      {/* ── Confirm dialog for lifecycle actions ── */}
      <ConfirmDialog
        busy={isActioning}
        cancelLabel="Back"
        confirmLabel={actionCopy?.confirmLabel ?? 'Confirm'}
        description={actionCopy?.description}
        onCancel={closeDialog}
        onConfirm={handleActionConfirm}
        open={Boolean(pendingAction)}
        title={actionCopy?.title ?? ''}
        tone={pendingAction === 'cancel' ? 'danger' : 'primary'}
      >
        {actionError ? (
          <FeedbackBanner tone="error">{actionError}</FeedbackBanner>
        ) : null}
      </ConfirmDialog>
    </DashboardLayout>
  )
}

export default OrganizerTripDetailPage
