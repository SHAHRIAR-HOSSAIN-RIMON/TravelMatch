import { Link, useLocation } from 'react-router-dom'
import styles from './Dashboard.module.css'

const navItems = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'My Trips', to: '/trips' },
  { label: 'Proposals', to: '/proposals' },
  { label: 'Organized Trips', to: '/organized-trips' },
  { label: 'Payments', to: '/payments' },
  { label: 'Profile', to: '/profile' },
]

function isActivePath(pathname, to) {
  return pathname === to || pathname.startsWith(`${to}/`)
}

function DashboardNav({ layout = 'row', onNavigate }) {
  const { pathname } = useLocation()
  const isStacked = layout === 'stack'

  return (
    <ul className={`${styles.nav} ${isStacked ? styles.navStack : ''}`} aria-label="Tourist navigation">
      {navItems.map((item) => {
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
