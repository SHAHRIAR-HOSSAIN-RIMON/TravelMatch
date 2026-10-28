import { guideBenefits } from './data.js'
import styles from './LandingPage.module.css'

function BecomeGuideSection() {
  return (
    <section className={styles.guideSection} id="become-guide">
      <div className={styles.guideGrid}>
        <div className={styles.guideCopy} data-reveal>
          <h2>Share Your City. Become a Guide.</h2>
          <p>
            Turn your local knowledge into meaningful travel experiences and connect with tourists
            looking for authentic adventures.
          </p>
          <a className={styles.guideButton} href="#footer">
            Become a Guide
          </a>
        </div>

        <ul className={styles.guideBenefits}>
          {guideBenefits.map((benefit) => (
            <li className={styles.guideBenefit} data-reveal key={benefit.title}>
              <span className={styles.guideDot} aria-hidden="true" />
              <div>
                <h3>{benefit.title}</h3>
                <p>{benefit.description}</p>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

export default BecomeGuideSection
