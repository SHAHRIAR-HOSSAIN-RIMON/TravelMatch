import styles from './LandingPage.module.css'

function SectionHeading({ title, description, align = 'center' }) {
  const headingClass =
    align === 'left'
      ? `${styles.sectionHeading} ${styles.sectionHeadingLeft}`
      : styles.sectionHeading

  return (
    <div className={headingClass}>
      <h2 className={styles.sectionTitle}>{title}</h2>
      {description ? (
        <p className={styles.sectionSubtitle}>{description}</p>
      ) : null}
    </div>
  )
}

export default SectionHeading
