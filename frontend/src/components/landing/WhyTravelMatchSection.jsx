import { benefits } from './data.js'
import SectionHeading from './SectionHeading.jsx'
import styles from './LandingPage.module.css'

function WhyTravelMatchSection() {
  return (
    <section className={styles.whySection} id="why-travelmatch">
      <SectionHeading
        title="Why TravelMatch?"
        description="Everything you need to plan, connect, and experience your journey with confidence."
      />
      <div className={styles.benefitGrid}>
        {benefits.map((benefit) => (
          <article className={styles.benefitItem} data-reveal key={benefit.title}>
            <span className={styles.benefitInitial} aria-hidden="true">
              {benefit.initial}
            </span>
            <h3>{benefit.title}</h3>
            <p>{benefit.description}</p>
          </article>
        ))}
      </div>
    </section>
  )
}

export default WhyTravelMatchSection
