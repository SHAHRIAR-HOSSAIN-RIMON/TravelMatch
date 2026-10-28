import { useRef, useState } from 'react'
import checkIcon from '../../assets/auth-role-check.svg'
import chevronIcon from '../../assets/auth-chevron.svg'
import guideIcon from '../../assets/auth-role-guide.svg'
import organizerIcon from '../../assets/auth-role-organizer.svg'
import touristIcon from '../../assets/auth-role-tourist.svg'
import styles from './Auth.module.css'

const roles = [
  {
    id: 'tourist',
    label: 'Tourist',
    description: 'Discover spots & meet guides',
    icon: touristIcon,
  },
  {
    id: 'local-guide',
    label: 'Local Guide',
    description: 'Share spots & earn money',
    icon: guideIcon,
  },
  {
    id: 'tour-organizer',
    label: 'Tour Organizer',
    description: 'Host and manage group trips',
    icon: organizerIcon,
  },
]

function RoleSelect({ value, onChange, error }) {
  const [open, setOpen] = useState(true)
  const optionRefs = useRef([])
  const selected = roles.find((role) => role.id === value) ?? roles[0]

  function selectRole(roleId) {
    onChange(roleId)
    setOpen(false)
  }

  function handleOptionKeyDown(event, index) {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return

    event.preventDefault()
    const direction = event.key === 'ArrowDown' ? 1 : -1
    const nextIndex = (index + direction + roles.length) % roles.length
    optionRefs.current[nextIndex]?.focus()
  }

  return (
    <div className={styles.roleSection}>
      <span className={styles.roleLabel} id="role-group-label">
        Select Your Role
      </span>

      <div className={`${styles.rolePanel} ${error ? styles.rolePanelError : ''}`}>
        <button
          aria-controls="signup-role-options"
          aria-expanded={open}
          className={styles.roleHeader}
          onClick={() => setOpen((current) => !current)}
          type="button"
        >
          <span>{selected.label}</span>
          <img
            className={`${styles.roleChevron} ${open ? styles.roleChevronOpen : ''}`}
            src={chevronIcon}
            alt=""
          />
        </button>

        {open ? (
          <div
            aria-describedby={error ? 'role-error' : undefined}
            aria-labelledby="role-group-label"
            className={styles.roleOptions}
            id="signup-role-options"
            role="radiogroup"
          >
            {roles.map((role, index) => {
              const isSelected = role.id === value

              return (
                <button
                  aria-checked={isSelected}
                  className={`${styles.roleOption} ${
                    isSelected ? styles.roleOptionSelected : ''
                  }`}
                  key={role.id}
                  onClick={() => selectRole(role.id)}
                  onKeyDown={(event) => handleOptionKeyDown(event, index)}
                  ref={(node) => {
                    optionRefs.current[index] = node
                  }}
                  role="radio"
                  tabIndex={0}
                  type="button"
                >
                  <span className={styles.roleIcon} aria-hidden="true">
                    <span
                      className={styles.roleIconGlyph}
                      style={{ '--role-icon': `url("${role.icon}")` }}
                    />
                  </span>
                  <span className={styles.roleText}>
                    <span className={styles.roleName}>{role.label}</span>
                    <span className={styles.roleDescription}>{role.description}</span>
                  </span>
                  {isSelected ? (
                    <span className={styles.roleCheck} aria-hidden="true">
                      <img src={checkIcon} alt="" />
                    </span>
                  ) : null}
                </button>
              )
            })}
          </div>
        ) : null}
      </div>

      {error ? (
        <p className={styles.fieldError} id="role-error" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  )
}

export default RoleSelect
