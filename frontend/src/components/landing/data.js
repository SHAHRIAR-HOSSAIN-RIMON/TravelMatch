import coxsBazarImage from '../../assets/coxs-bazar.png'
import rangamatiImage from '../../assets/rangamati.png'
import sajekValleyImage from '../../assets/sajek-valley.png'

export const destinations = [
  {
    id: 'rangamati',
    name: 'Rangamati',
    subtitle: 'Lakes & Hills',
    location: 'Rangamati, Bangladesh',
    image: rangamatiImage,
  },
  {
    id: 'coxs-bazar',
    name: "Cox's Bazar",
    subtitle: 'Sea & Serenity',
    location: "Cox's Bazar, Bangladesh",
    image: coxsBazarImage,
  },
  {
    id: 'sajek-valley',
    name: 'Sajek Valley',
    subtitle: 'Clouds & Hills',
    location: 'Sajek Valley, Bangladesh',
    image: sajekValleyImage,
  },
]

export const travelSteps = [
  {
    number: '01',
    title: 'Post Your Trip',
    description: 'Tell us your destination, travel dates, budget, and preferences.',
  },
  {
    number: '02',
    title: 'Find Your Guide',
    description:
      'Receive guide proposals and compare them based on budget fit, experience, and preferences.',
  },
  {
    number: '03',
    title: 'Travel & Track',
    description:
      'Confirm your guide, manage your itinerary, chat in real time, and track your journey.',
  },
]

export const benefits = [
  {
    initial: 'S',
    title: 'Smart Budget Matching',
    description: 'Find guides whose proposals fit your budget and travel preferences.',
  },
  {
    initial: 'V',
    title: 'Verified Local Guides',
    description: 'Connect with trusted guides and discover authentic local experiences.',
  },
  {
    initial: 'T',
    title: 'Real-Time Tracking',
    description: 'Follow your itinerary and trip checkpoints as your journey progresses.',
  },
  {
    initial: 'P',
    title: 'Secure Payments',
    description: 'Manage milestone-based payments through our secure sandbox wallet.',
  },
  {
    initial: 'C',
    title: 'Live Chat',
    description: 'Communicate directly with your guide throughout your trip.',
  },
  {
    initial: 'E',
    title: 'Personalized Experience',
    description: 'Build trips around your destination, budget, schedule, and preferences.',
  },
]

export const guideBenefits = [
  {
    title: 'Set Your Availability',
    description: 'Choose when and where you want to guide.',
  },
  {
    title: 'Receive Trip Proposals',
    description: 'Connect with tourists whose plans match your expertise.',
  },
  {
    title: 'Grow Your Experience',
    description: 'Build your guide profile through real travel experiences.',
  },
]

export const footerGroups = [
  {
    title: 'Explore',
    links: [
      { label: 'How It Works', href: '#how-it-works' },
      { label: 'Destinations', href: '#destinations' },
      { label: 'Find a Guide', href: '#destinations' },
      { label: 'Become a Guide', href: '#become-guide' },
    ],
  },
  {
    title: 'Company',
    links: [
      { label: 'About Us', href: '#why-travelmatch' },
      { label: 'Contact' },
      { label: 'Privacy Policy' },
      { label: 'Terms of Service' },
    ],
  },
]
