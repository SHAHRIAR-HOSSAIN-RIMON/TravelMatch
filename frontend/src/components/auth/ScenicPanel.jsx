import { Link } from 'react-router-dom'
import locationIcon from '../../assets/location-icon.svg'
import scenicPhoto from '../../assets/auth-scenic-panel.jpg'
import styles from './Auth.module.css'

function ScenicPanel({ variant = 'login' }) {
  const isSignup = variant === 'signup'

  return (
    <aside
      className={`${styles.scenicPanel} ${isSignup ? styles.scenicPanelSignup : ''}`}
      aria-label="TravelMatch — Travel freely, explore locally"
    >
      <img className={styles.scenicPhoto} src={scenicPhoto} alt="" />
      <div className={styles.scenicShade} aria-hidden="true" />

      <div className={styles.scenicContent}>
        <Link className={styles.brand} to="/" aria-label="TravelMatch home">
          <span className={styles.brandBadge} aria-hidden="true">
            <span className={styles.brandMark} />
          </span>
          <span className={styles.brandName}>
            Travel
            <span className={isSignup ? styles.brandNamePlain : styles.brandMatch}>Match</span>
          </span>
        </Link>

        <div
          className={`${styles.scenicMessage} ${isSignup ? styles.scenicMessageSignup : ''}`}
        >
          {!isSignup ? <span className={styles.scenicRule} aria-hidden="true" /> : null}
          <p className={`${styles.scenicHeadline} ${isSignup ? styles.scenicHeadlineSans : ''}`}>
            <span className={styles.headlineLine}>
              <span className={isSignup ? styles.headlineSans : styles.headlineSerif}>Travel</span>{' '}
              <span className={styles.headlineScript}>freely.</span>
            </span>
            <span className={styles.headlineLine}>
              <span className={isSignup ? styles.headlineSans : styles.headlineSerif}>
                Explore locally.
              </span>
            </span>
          </p>
          <p className={styles.scenicDescription}>
            {isSignup
              ? 'Create an account to match with local guides, discover hidden off-beat spots, and join our global community of conscious travelers.'
              : 'Meaningful travel experiences start with real people.'}
          </p>
        </div>

        <div className={styles.scenicFooter}>
          <div className={styles.scenicLocation}>
            <span
              className={`${styles.scenicLocationLine} ${
                isSignup ? styles.scenicLocationPill : ''
              }`}
            >
              <img src={locationIcon} alt="" />
              Rangamati, Bangladesh
            </span>
            {!isSignup ? (
              <span className={styles.scenicTagline}>Find stories everywhere.</span>
            ) : null}
          </div>
          <span className={`${styles.scenicScript} ${isSignup ? styles.scenicScriptPlain : ''}`}>
            More than a trip
          </span>
        </div>
      </div>
    </aside>
  )
}

export default ScenicPanel
