import BecomeGuideSection from './BecomeGuideSection.jsx'
import DestinationsSection from './DestinationsSection.jsx'
import FinalCtaSection from './FinalCtaSection.jsx'
import HeroSection from './HeroSection.jsx'
import HowItWorksSection from './HowItWorksSection.jsx'
import SiteFooter from './SiteFooter.jsx'
import WhyTravelMatchSection from './WhyTravelMatchSection.jsx'

function LandingPage() {
  return (
    <div className="landing-page">
      <main>
        <HeroSection />
        <HowItWorksSection />
        <WhyTravelMatchSection />
        <DestinationsSection />
        <BecomeGuideSection />
        <FinalCtaSection />
      </main>
      <SiteFooter />
    </div>
  )
}

export default LandingPage
