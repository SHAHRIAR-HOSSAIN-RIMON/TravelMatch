import airplaneBackground from '../../assets/airplane-background.png'
import { travelSteps } from './data.js'
import SectionHeading from './SectionHeading.jsx'
import styles from './LandingPage.module.css'

function HowItWorksSection() {
  return (
    <section className={styles.howSection} id="how-it-works">
      <img className={styles.planeArtwork} src={airplaneBackground} alt="" />
      <SectionHeading
        title="How TravelMatch Works"
        description="From planning your trip to exploring your destination, TravelMatch keeps your journey simple."
      />
      <div className={styles.stepGrid}>
        {travelSteps.map((step) => (
          <article className={styles.stepItem} data-reveal key={step.number}>
            <span className={styles.stepNumber}>{step.number}</span>
            <h3>{step.title}</h3>
            <p>{step.description}</p>
          </article>
        ))}
      </div>
    </section>
  )
}

export default HowItWorksSection
