import { useEffect, useState } from 'react';
import { NavLink, Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import LanguageSwitcher from './LanguageSwitcher';
import { authApi } from '../../api/endpoints';
import { useAuthStore } from '../../store/authStore';

const LINKS = [
  { to: '/', key: 'nav.home', end: true },
  { to: '/reserver', key: 'nav.book' },
  { to: '/devis', key: 'nav.charter' },
  { to: '/nos-avions', key: 'nav.fleet' },
  { to: '/nos-services', key: 'nav.services' },
  { to: '/qui-sommes-nous', key: 'nav.about' },
  { to: '/contacts', key: 'nav.contact' },
];

export default function Header() {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  const { accessToken, clearSession } = useAuthStore();

  const close = () => setOpen(false);

  // Any navigation closes the drawer — browser back and links elsewhere on the
  // page included, not only the ones rendered here.
  useEffect(close, [location.pathname]);

  async function signOut() {
    // Clear locally regardless of what the server says: the user asked to be
    // signed out, and a failed request must not leave them looking signed in.
    try {
      await authApi.logout();
    } finally {
      clearSession();
      navigate('/');
    }
  }

  return (
    <header className="header">
      <div className="shell header__inner">
        <Link to="/" className="header__logo" onClick={close} aria-label="Malu Aviation">
          <img src="/images/logo.png" alt="Malu Aviation" />
        </Link>

        <nav className="header__nav" aria-label={t('nav.menu')}>
          {LINKS.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.end}
              className={({ isActive }) => (isActive ? 'is-active' : undefined)}
            >
              {t(link.key)}
            </NavLink>
          ))}
        </nav>

        <div className="header__actions">
          {accessToken ? (
            <>
              <Link to="/mes-reservations" className="header__account">
                {t('nav.myBookings')}
              </Link>
              <button type="button" className="link-button" onClick={signOut}>
                {t('nav.signOut')}
              </button>
            </>
          ) : (
            <Link to="/connexion" className="header__account">
              {t('nav.signIn')}
            </Link>
          )}

          <LanguageSwitcher />

          <button
            type="button"
            className="burger"
            aria-expanded={open}
            aria-controls="mobile-nav"
            onClick={() => setOpen((value) => !value)}
          >
            {t('nav.menu')}
          </button>
        </div>
      </div>

      {open && (
        <nav id="mobile-nav" className="mobile-nav" aria-label={t('nav.menu')}>
          <div className="shell">
            {LINKS.map((link) => (
              <Link key={link.to} to={link.to} onClick={close}>
                {t(link.key)}
              </Link>
            ))}
            <Link to={accessToken ? '/mes-reservations' : '/connexion'} onClick={close}>
              {t(accessToken ? 'nav.myBookings' : 'nav.signIn')}
            </Link>
          </div>
        </nav>
      )}
    </header>
  );
}
