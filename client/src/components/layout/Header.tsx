import { useEffect, useState } from 'react';
import { NavLink, Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Logo from '../brand/Logo';
import Icon from '../ui/Icon';
import LanguageSwitcher from './LanguageSwitcher';
import FlightSearchCard from '../search/FlightSearchCard';
import { authApi } from '../../api/endpoints';
import { useAuthStore } from '../../store/authStore';

const LINKS = [
  { to: '/', key: 'nav.home', end: true },
  { to: '/reserver', key: 'nav.flights' },
  { to: '/qui-sommes-nous', key: 'nav.about' },
  { to: '/nos-services', key: 'nav.services' },
  { to: '/nos-avions', key: 'nav.fleet' },
  { to: '/contacts', key: 'nav.contact' },
];

export default function Header() {
  const { t } = useTranslation();
  const [menuOpen, setMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  const { accessToken, clearSession } = useAuthStore();

  function closeAll() {
    setMenuOpen(false);
    setSearchOpen(false);
  }

  // Any navigation closes both panels — browser back and links elsewhere on
  // the page included, not only the ones rendered here.
  useEffect(closeAll, [location.pathname, location.search]);

  // The bar only lifts off the page once there is something underneath it.
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    if (!menuOpen && !searchOpen) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closeAll();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [menuOpen, searchOpen]);

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
    <header className={scrolled ? 'header is-scrolled' : 'header'}>
      <div className="shell header__inner">
        <Link to="/" onClick={closeAll} aria-label={t('brand.name')}>
          <Logo />
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
          <button
            type="button"
            className="icon-btn"
            aria-expanded={searchOpen}
            aria-controls="header-search"
            aria-label={t('search.title')}
            onClick={() => {
              setSearchOpen((open) => !open);
              setMenuOpen(false);
            }}
          >
            <Icon name={searchOpen ? 'close' : 'search'} size={19} />
          </button>

          <LanguageSwitcher />

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

          <Link to="/reserver" className="btn btn--ghost btn--sm">
            {t('nav.bookFlight')}
          </Link>

          <button
            type="button"
            className="icon-btn burger"
            aria-expanded={menuOpen}
            aria-controls="mobile-nav"
            aria-label={t('nav.menu')}
            onClick={() => {
              setMenuOpen((open) => !open);
              setSearchOpen(false);
            }}
          >
            <Icon name={menuOpen ? 'close' : 'menu'} size={21} />
          </button>
        </div>
      </div>

      {searchOpen && (
        <div id="header-search" className="header__panel">
          <div className="shell">
            <h2>{t('search.title')}</h2>
            <FlightSearchCard variant="flat" onSubmitted={closeAll} />
          </div>
        </div>
      )}

      {menuOpen && (
        <nav id="mobile-nav" className="mobile-nav" aria-label={t('nav.menu')}>
          <div className="shell">
            {LINKS.map((link) => (
              <Link key={link.to} to={link.to} onClick={closeAll}>
                {t(link.key)}
              </Link>
            ))}
            <Link to="/devis" onClick={closeAll}>
              {t('nav.charter')}
            </Link>
            <Link to={accessToken ? '/mes-reservations' : '/connexion'} onClick={closeAll}>
              {t(accessToken ? 'nav.myBookings' : 'nav.signIn')}
            </Link>

            <Link to="/reserver" className="btn btn--primary" onClick={closeAll}>
              {t('nav.bookFlight')}
              <Icon name="arrow-right" size={17} />
            </Link>
          </div>
        </nav>
      )}
    </header>
  );
}
