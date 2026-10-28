import { Link } from 'react-router-dom'
import styles from './LandingPage.module.css'

function FinalCtaSection() {
  return (
    <section className={styles.finalSection} id="final-cta" aria-labelledby="final-cta-title">
      <div className={styles.finalContent} data-reveal>
        <h2 id="final-cta-title">Ready to Travel Freely?</h2>
        <p>
          Find a local guide, plan your journey, and experience your destination your way.
        </p>
        <Link className={styles.finalButton} to="/signup">
          Get Started
        </Link>
      </div>
    </section>
  )
}

export default FinalCtaSection
