import { destinations } from './data.js'
import DestinationCard from './DestinationCard.jsx'
import SectionHeading from './SectionHeading.jsx'
import styles from './LandingPage.module.css'

function DestinationsSection() {
  return (
    <section className={styles.destinationsSection} id="destinations">
      <SectionHeading
        title="Explore Popular Destinations"
        description="Discover beautiful places and connect with local guides who know them best."
      />
      <div className={styles.destinationGrid}>
        {destinations.map((destination) => (
          <div data-reveal key={destination.id}>
            <DestinationCard destination={destination} />
          </div>
        ))}
      </div>
    </section>
  )
}

export default DestinationsSection
