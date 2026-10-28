import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import mark from '../../assets/travelmatch-mark.svg'
import searchIcon from '../../assets/search-icon.svg'
import styles from './LandingPage.module.css'

const navigation = [
  { label: 'Explore', href: '#destinations', isActive: true },
  { label: 'How It Works', href: '#how-it-works' },
  { label: 'About', href: '#why-travelmatch' },
  { label: 'Contact' },
]

function SiteHeader() {
  const [menuOpen, setMenuOpen] = useState(false)
  const headerRef = useRef(null)
  const menuButtonRef = useRef(null)

  useEffect(() => {
    if (!menuOpen) return undefined

    function handlePointerDown(event) {
      if (!headerRef.current?.contains(event.target)) {
        setMenuOpen(false)
      }
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        setMenuOpen(false)
        menuButtonRef.current?.focus()
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [menuOpen])

  function closeMenu() {
    setMenuOpen(false)
  }

  return (
    <header className={styles.header} ref={headerRef}>
      <div className={styles.headerInner}>
        <a
          className={styles.brand}
          href="#top"
          aria-label="TravelMatch home"
          onClick={closeMenu}
        >
          <img className={styles.brandMark} src={mark} alt="" />
          <span className={styles.brandWordmark}>
            Travel<span className={styles.brandAccent}>Match</span>
          </span>
        </a>

        <nav
          id="mobile-primary-navigation"
          className={`${styles.desktopNav} ${menuOpen ? styles.mobileNavOpen : ''}`}
          aria-label="Main navigation"
        >
          {navigation.map((item) =>
            item.href ? (
              <a
                className={`${styles.navLink} ${item.isActive ? styles.navLinkActive : ''}`}
                href={item.href}
                key={item.label}
                onClick={closeMenu}
              >
                {item.label}
              </a>
            ) : (
              <span
                aria-disabled="true"
                className={`${styles.navLink} ${styles.navLinkUnavailable}`}
                key={item.label}
              >
                {item.label}
              </span>
            ),
          )}
          <Link className={styles.mobileLoginLabel} to="/login" onClick={closeMenu}>
            Log in
          </Link>
        </nav>

        <div className={styles.headerActions}>
          <a className={styles.searchLink} href="#destinations" aria-label="Explore destinations">
            <img src={searchIcon} alt="" />
          </a>
          <Link className={styles.loginLabel} to="/login">
            Log in
          </Link>
          <Link className={styles.headerCta} to="/signup">
            Get Started
          </Link>
          <button
            aria-controls="mobile-primary-navigation"
            aria-expanded={menuOpen}
            aria-label={menuOpen ? 'Close navigation' : 'Open navigation'}
            className={styles.menuToggle}
            onClick={() => setMenuOpen((open) => !open)}
            ref={menuButtonRef}
            type="button"
          >
            <span />
            <span />
            <span />
          </button>
        </div>
      </div>
    </header>
  )
}

export default SiteHeader
