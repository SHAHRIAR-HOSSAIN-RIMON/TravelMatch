import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/useAuth.js'
import styles from './Dashboard.module.css'

const touristNavItems = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'My Trips', to: '/trips' },
  { label: 'Proposals', to: '/proposals' },
  { label: 'Organized Trips', to: '/organized-trips' },
  { label: 'Payments', to: '/payments' },
  { label: 'Profile', to: '/profile' },
]

const guideNavItems = [
  { label: 'Dashboard', to: '/guide/dashboard' },
  { label: 'Trip Requests', to: '/guide/trip-requests' },
  { label: 'My Proposals', to: '/guide/proposals' },
  { label: 'My Trips', to: '/guide/trips' },
  { label: 'Profile', to: '/profile' },
]

const adminNavItems = [
  { label: 'Dashboard', to: '/admin/dashboard' },
  { label: 'Users', to: '/admin/users' },
  { label: 'Verifications', to: '/admin/verifications' },
  { label: 'Disputes', to: '/admin/disputes' },
  { label: 'Profile', to: '/profile' },
]

function isActivePath(pathname, to) {
  return pathname === to || pathname.startsWith(`${to}/`)
}

function DashboardNav({ items, layout = 'row', onNavigate }) {
  const { pathname } = useLocation()
  const { user } = useAuth()
  const isStacked = layout === 'stack'

  const isAdmin = user?.role === 'Admin'
  const isGuide = user?.role === 'Guide'
  const resolvedItems =
    items || (isAdmin ? adminNavItems : isGuide ? guideNavItems : touristNavItems)
  const navAriaLabel = isAdmin
    ? 'Admin navigation'
    : isGuide
    ? 'Guide navigation'
    : 'Tourist navigation'

  return (
    <ul className={`${styles.nav} ${isStacked ? styles.navStack : ''}`} aria-label={navAriaLabel}>
      {resolvedItems.map((item) => {
        const isCurrent = isActivePath(pathname, item.to)

        return (
          <li className={styles.navItem} key={item.label}>
            <Link
              aria-current={isCurrent ? 'page' : undefined}
              className={`${styles.navLink} ${isCurrent ? styles.navLinkActive : ''}`}
              onClick={onNavigate}
              to={item.to}
            >
              {item.label}
            </Link>
          </li>
        )
      })}
    </ul>
  )
}

export default DashboardNav
