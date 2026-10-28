import { footerGroups } from './data.js'
import styles from './LandingPage.module.css'

function FooterLink({ link }) {
  if (!link.href) return <span>{link.label}</span>

  return <a href={link.href}>{link.label}</a>
}

function SiteFooter() {
  return (
    <footer className={styles.footer} id="footer">
      <div className={styles.footerInner}>
        <div className={styles.footerGrid}>
          <div className={styles.footerBrand}>
            <a href="#top" className={styles.footerBrandTitle}>
              TravelMatch
            </a>
            <p>Travel freely. Explore locally.</p>
          </div>

          {footerGroups.map((group) => (
            <nav aria-label={group.title} className={styles.footerGroup} key={group.title}>
              <h2>{group.title}</h2>
              <ul>
                {group.links.map((link) => (
                  <li key={link.label}>
                    <FooterLink link={link} />
                  </li>
                ))}
              </ul>
            </nav>
          ))}

          <div className={styles.footerProduct}>
            <h2>TravelMatch</h2>
            <p>Intelligent Local Guide &amp; Trip Marketplace</p>
            <p className={styles.footerProductDetail}>
              Real-time itinerary tracking • Live chat • Budget-fit matching
            </p>
          </div>
        </div>

        <div className={styles.footerDivider} />
        <p className={styles.copyright}>© 2026 TravelMatch. All rights reserved.</p>
      </div>
    </footer>
  )
}

export default SiteFooter
