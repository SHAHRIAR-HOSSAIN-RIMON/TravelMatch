import { useCallback, useEffect, useRef, useState } from 'react'
import DashboardLayout from '../../components/dashboard/DashboardLayout.jsx'
import FeedbackBanner from '../../components/tourist/FeedbackBanner.jsx'
import ConfirmDialog from '../../components/tourist/ConfirmDialog.jsx'
import { useAuth } from '../../context/useAuth.js'
import { getUsers, updateUserStatus } from '../../lib/adminService.js'
import { describeApiError } from '../../lib/api.js'
import styles from '../../components/admin/Admin.module.css'

function formatDate(isoString) {
  if (!isoString) return '—'
  try {
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(isoString))
  } catch {
    return String(isoString)
  }
}

export function AdminUsersPage() {
  const { user, token, logout } = useAuth()

  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [feedback, setFeedback] = useState(null)

  // Filters & Pagination
  const [roleFilter, setRoleFilter] = useState('All')
  const [statusFilter, setStatusFilter] = useState('All')
  const [page, setPage] = useState(1)
  const [pageSize] = useState(15)
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)

  // Toggle status dialog state
  const [selectedUser, setSelectedUser] = useState(null)
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false)

  const requestIdRef = useRef(0)

  const fetchUsers = useCallback(
    async (currentPage = page) => {
      if (!token) return

      const requestId = ++requestIdRef.current
      setLoading(true)
      setError(null)

      try {
        const params = {
          page: currentPage,
          pageSize,
        }
        if (roleFilter !== 'All') params.role = roleFilter
        if (statusFilter === 'Active') params.isActive = true
        if (statusFilter === 'Inactive') params.isActive = false

        const result = await getUsers(token, params)
        if (requestId !== requestIdRef.current) return
        setUsers(result.data || [])
        setTotalCount(result.totalCount || 0)
        setTotalPages(result.totalPages || 0)
        setPage(result.page || currentPage)
      } catch (err) {
        if (requestId !== requestIdRef.current) return
        setError(describeApiError(err, 'Failed to load user directory.'))
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false)
        }
      }
    },
    [page, pageSize, roleFilter, statusFilter, token],
  )

  useEffect(() => {
    const requestId = ++requestIdRef.current
    let cancelled = false

    ;(async () => {
      await fetchUsers(1)
      if (cancelled || requestId !== requestIdRef.current) return
    })()

    return () => {
      cancelled = true
    }
  }, [fetchUsers])

  async function handleConfirmStatusToggle() {
    if (!token || !selectedUser) return

    const newStatus = !selectedUser.isActive
    setIsUpdatingStatus(true)
    setFeedback(null)

    try {
      await updateUserStatus(token, selectedUser.id, newStatus)
      setFeedback({
        tone: 'success',
        message: `User ${selectedUser.fullName || selectedUser.email} has been ${
          newStatus ? 'activated' : 'deactivated'
        }.`,
      })
      setSelectedUser(null)
      fetchUsers(page)
    } catch (err) {
      setFeedback({
        tone: 'error',
        message: describeApiError(err, 'Failed to update user status.'),
      })
    } finally {
      setIsUpdatingStatus(false)
    }
  }

  function getRolePill(role) {
    const norm = (role || '').toLowerCase()
    let className = styles.roleTourist
    if (norm === 'guide') className = styles.roleGuide
    else if (norm === 'organizer') className = styles.roleOrganizer
    else if (norm === 'admin') className = styles.roleAdmin

    return <span className={`${styles.rolePill} ${className}`}>{role}</span>
  }

  return (
    <DashboardLayout onLogout={logout} user={user}>
      <div className={styles.container}>
        <div className={styles.header}>
          <h1 className={styles.title}>User Management</h1>
          <p className={styles.subtitle}>
            Browse, filter, and moderate account access across all platform users.
          </p>
        </div>

        {feedback && (
          <div style={{ marginBottom: 20 }}>
            <FeedbackBanner tone={feedback.tone}>{feedback.message}</FeedbackBanner>
          </div>
        )}

        {error && (
          <div style={{ marginBottom: 20 }}>
            <FeedbackBanner tone="error">{error}</FeedbackBanner>
          </div>
        )}

        {/* Filter Controls */}
        <div className={styles.filterBar}>
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel} htmlFor="role-filter">
              Filter by Role:
            </label>
            <select
              className={styles.select}
              id="role-filter"
              onChange={(e) => {
                setRoleFilter(e.target.value)
                setPage(1)
              }}
              value={roleFilter}
            >
              <option value="All">All Roles</option>
              <option value="Tourist">Tourist</option>
              <option value="Guide">Guide</option>
              <option value="Organizer">Organizer</option>
              <option value="Admin">Admin</option>
            </select>
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel} htmlFor="status-filter">
              Account Status:
            </label>
            <select
              className={styles.select}
              id="status-filter"
              onChange={(e) => {
                setStatusFilter(e.target.value)
                setPage(1)
              }}
              value={statusFilter}
            >
              <option value="All">All Statuses</option>
              <option value="Active">Active only</option>
              <option value="Inactive">Inactive only</option>
            </select>
          </div>

          <div style={{ marginLeft: 'auto', fontSize: 13, color: 'var(--tm-slate-500)' }}>
            Total: <strong>{totalCount}</strong> user{totalCount === 1 ? '' : 's'}
          </div>
        </div>

        {/* User Table */}
        {loading ? (
          <div className={styles.tableWrap} style={{ padding: 32, textAlign: 'center', color: 'var(--tm-slate-500)' }}>
            Loading users…
          </div>
        ) : users.length === 0 ? (
          <div className={styles.emptyState}>
            <h3 className={styles.emptyTitle}>No users found</h3>
            <p className={styles.emptyText}>
              No accounts match the current role or status filters.
            </p>
          </div>
        ) : (
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>User ID</th>
                  <th>Full Name</th>
                  <th>Email Address</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Joined Date</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {users.map((item) => {
                  const isSelf = user?.id === item.id
                  return (
                    <tr key={item.id}>
                      <td style={{ fontWeight: 600 }}>#{item.id}</td>
                      <td>
                        <strong>{item.fullName || '—'}</strong>
                        {isSelf && (
                          <span style={{ marginLeft: 6, fontSize: 11, color: 'var(--tm-teal-700)', fontWeight: 600 }}>
                            (You)
                          </span>
                        )}
                      </td>
                      <td>{item.email}</td>
                      <td>{getRolePill(item.role)}</td>
                      <td>
                        {item.isActive ? (
                          <span className={styles.activePill}>
                            <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#059669' }} />
                            Active
                          </span>
                        ) : (
                          <span className={styles.inactivePill}>
                            <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#64748b' }} />
                            Inactive
                          </span>
                        )}
                      </td>
                      <td>{formatDate(item.createdAt)}</td>
                      <td>
                        <button
                          className={`${styles.actionBtn} ${
                            item.isActive ? styles.actionBtnDanger : styles.actionBtnSuccess
                          }`}
                          disabled={isSelf}
                          onClick={() => setSelectedUser(item)}
                          title={isSelf ? 'You cannot deactivate your own admin account.' : undefined}
                          type="button"
                        >
                          {item.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className={styles.pagination}>
            <button
              className={styles.pageButton}
              disabled={page <= 1 || loading}
              onClick={() => fetchUsers(page - 1)}
              type="button"
            >
              ← Previous
            </button>
            <span className={styles.pageInfo}>
              Page {page} of {totalPages}
            </span>
            <button
              className={styles.pageButton}
              disabled={page >= totalPages || loading}
              onClick={() => fetchUsers(page + 1)}
              type="button"
            >
              Next →
            </button>
          </div>
        )}

        {/* Status Confirmation Dialog */}
        <ConfirmDialog
          busy={isUpdatingStatus}
          confirmLabel={selectedUser?.isActive ? 'Deactivate User' : 'Activate User'}
          description={
            selectedUser?.isActive
              ? `Are you sure you want to deactivate ${selectedUser.fullName || selectedUser.email}? They will no longer be able to log in or use the platform.`
              : `Are you sure you want to activate ${selectedUser?.fullName || selectedUser?.email}? Their account access will be restored.`
          }
          onCancel={() => setSelectedUser(null)}
          onConfirm={handleConfirmStatusToggle}
          open={Boolean(selectedUser)}
          title={selectedUser?.isActive ? 'Deactivate Account' : 'Activate Account'}
          tone={selectedUser?.isActive ? 'danger' : 'primary'}
        />
      </div>
    </DashboardLayout>
  )
}

export default AdminUsersPage

