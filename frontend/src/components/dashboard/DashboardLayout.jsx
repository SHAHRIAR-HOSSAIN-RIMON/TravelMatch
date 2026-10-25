import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import DashboardNav from './DashboardNav.jsx'
import { getInitials } from './dashboardModel.js'
import styles from './Dashboard.module.css'

function DashboardLayout({ user, onLogout, children }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const headerRef = useRef(null)
  const toggleRef = useRef(null)

  useEffect(() => {
    if (!menuOpen) return undefined

    function handlePointerDown(event) {
      if (headerRef.current?.contains(event.target)) return
      setMenuOpen(false)
    }

    function handleKeyDown(event) {
      if (event.key !== 'Escape') return
      setMenuOpen(false)
      toggleRef.current?.focus()
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [menuOpen])

  const fullName = user?.fullName ?? 'TravelMatch traveller'
  const initials = getInitials(user?.fullName)

  function closeMenu() {
    setMenuOpen(false)
  }

  function handleLogout() {
    setMenuOpen(false)
    onLogout()
  }

  return (
    <div className={styles.page}>
      <header className={styles.header} ref={headerRef}>
        <div className={styles.headerInner}>
          <Link className={styles.brand} to="/" aria-label="TravelMatch home">
            <span className={styles.brandMark} aria-hidden="true" />
            <span className={styles.brandWordmark}>
              Travel<span className={styles.brandAccent}>Match</span>
            </span>
          </Link>

          <div className={`${styles.navWrap} ${styles.desktopNav}`}>
            <DashboardNav />
          </div>

          <div className={styles.userCluster}>
            <span className={styles.avatar} aria-hidden="true">
              {initials}
            </span>
            <span className={styles.userName}>{fullName}</span>

            <button className={styles.logoutButton} onClick={handleLogout} type="button">
              <span className={styles.logoutIcon} aria-hidden="true" />
              Log out
            </button>

            <button
              aria-controls="dashboard-mobile-navigation"
              aria-expanded={menuOpen}
              aria-label={menuOpen ? 'Close navigation' : 'Open navigation'}
              className={styles.menuToggle}
              onClick={() => setMenuOpen((open) => !open)}
              ref={toggleRef}
              type="button"
            >
              <span className={menuOpen ? styles.iconClose : styles.iconMenu} aria-hidden="true" />
            </button>
          </div>
        </div>

        <div
          className={`${styles.mobilePanel} ${menuOpen ? styles.mobilePanelOpen : ''}`}
          id="dashboard-mobile-navigation"
        >
          <DashboardNav layout="stack" onNavigate={closeMenu} />

          <div className={styles.mobileUser}>
            <span className={styles.avatar} aria-hidden="true">
              {initials}
            </span>
            <span className={styles.mobileUserName}>{fullName}</span>
          </div>

          <button className={styles.mobileLogout} onClick={handleLogout} type="button">
            <span className={styles.logoutIcon} aria-hidden="true" />
            Log out
          </button>
        </div>
      </header>

      <main className={styles.main}>{children}</main>

      <footer className={styles.footer}>
        <div className={styles.footerInner}>© 2026 TravelMatch. All rights reserved.</div>
      </footer>
    </div>
  )
}

export default DashboardLayout
