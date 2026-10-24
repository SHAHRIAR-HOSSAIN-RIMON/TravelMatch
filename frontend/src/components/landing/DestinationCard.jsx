import styles from './LandingPage.module.css'

function DestinationCard({ destination }) {
  return (
    <article className={styles.destinationCard}>
      <img
        className={styles.destinationImage}
        src={destination.image}
        alt={`${destination.name} landscape`}
        loading="eager"
      />
      <div className={styles.destinationOverlay}>
        <h3 className={styles.destinationName}>{destination.name}</h3>
        <p className={styles.destinationType}>{destination.subtitle}</p>
      </div>
    </article>
  )
}

export default DestinationCard
