import { useState } from 'react'
import arrowLeft from '../../assets/arrow-left.svg'
import arrowRight from '../../assets/arrow-right.svg'
import bookmarkIcon from '../../assets/bookmark-icon.svg'
import cardArrow from '../../assets/card-arrow.svg'
import heroLocationIcon from '../../assets/hero-location-accent.svg'
import locationIcon from '../../assets/location-icon.svg'
import { destinations } from './data.js'
import SiteHeader from './SiteHeader.jsx'
import styles from './LandingPage.module.css'

function HeroSection() {
  const [activeIndex, setActiveIndex] = useState(0)
  const activeDestination = destinations[activeIndex]
  const orderedDestinations = [
    ...destinations.slice(activeIndex),
    ...destinations.slice(0, activeIndex),
  ]

  function showPrevious() {
    setActiveIndex((current) => (current + destinations.length - 1) % destinations.length)
  }

  function showNext() {
    setActiveIndex((current) => (current + 1) % destinations.length)
  }

  return (
    <section className={styles.heroSection} id="top" aria-label="TravelMatch introduction">
      <SiteHeader />
      <div className={styles.heroBody}>
        <div className={styles.heroCopy} data-reveal>
          <div className={styles.heroEyebrow}>
            <span className={styles.eyebrowLine} />
            <span>More than a trip</span>
          </div>
          <h1 className={styles.heroTitle}>
            Travel <span>Freely.</span>
          </h1>
          <p className={styles.heroDescription}>
            Connect with local guides, explore new places, and experience the world your way.
          </p>
          <div className={styles.heroCtas}>
            <a className={styles.primaryButton} href="#destinations">
              Explore Destinations
              <img src={arrowRight} alt="" />
            </a>
            <a className={styles.secondaryButton} href="#become-guide">
              Become a Guide
            </a>
          </div>
        </div>

        <div className={styles.heroDeck} role="group" aria-label="Featured destinations">
          {orderedDestinations.map((destination, index) => (
            <article
              className={`${styles.heroCard} ${styles[`heroCard${index + 1}`]}`}
              key={destination.id}
              style={{ backgroundImage: `linear-gradient(180deg, rgba(0,0,0,.18), rgba(0,0,0,.12)), url("${destination.image}")` }}
            >
              <div className={styles.heroCardHeader}>
                <div className={styles.heroLocation}>
                  <img className={styles.cardLocationIcon} src={locationIcon} alt="" />
                  <div className={styles.heroCardText}>
                    <h2>{destination.name}</h2>
                    <p>{destination.subtitle}</p>
                  </div>
                </div>
                <img className={styles.bookmarkIcon} src={bookmarkIcon} alt="" />
              </div>
              <a
                className={styles.heroCardLink}
                href="#destinations"
                aria-label={`Explore ${destination.name}`}
              >
                <img src={cardArrow} alt="" />
              </a>
            </article>
          ))}
        </div>
      </div>

      <div className={styles.heroFooter}>
        <div className={styles.heroLocationInfo} aria-live="polite">
          <div className={styles.heroFooterLocation}>
            <img src={heroLocationIcon} alt="" />
            <span>{activeDestination.location}</span>
          </div>
          <p className={styles.heroHandwritten}>Find stories everywhere...</p>
        </div>

        <a className={styles.scrollCue} href="#how-it-works" aria-label="Scroll to explore">
          <span className={styles.scrollMouse} aria-hidden="true">
            <span />
          </span>
          <span className={styles.scrollText}>Scroll to explore</span>
        </a>

        <div className={styles.heroPagination} aria-label="Featured destination carousel">
          <div className={styles.paginationArrows}>
            <button
              aria-label="Show previous destination"
              className={styles.paginationButton}
              onClick={showPrevious}
              type="button"
            >
              <img src={arrowLeft} alt="" />
            </button>
            <button
              aria-label="Show next destination"
              className={styles.paginationButton}
              onClick={showNext}
              type="button"
            >
              <img src={arrowRight} alt="" />
            </button>
          </div>
          <div className={styles.paginationIndicator} aria-live="polite">
            <span className={styles.paginationCurrent} aria-label={`Slide ${activeIndex + 1}`}>
              {String(activeIndex + 1).padStart(2, '0')}
            </span>
            <span className={styles.paginationLine} />
            <span className={styles.paginationTotal}>03</span>
          </div>
        </div>
      </div>
    </section>
  )
}

export default HeroSection
