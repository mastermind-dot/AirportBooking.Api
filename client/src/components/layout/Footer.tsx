import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Logo from '../brand/Logo';
import Icon from '../ui/Icon';

export default function Footer() {
  const { t } = useTranslation();
  const year = new Date().getFullYear();

  return (
    <footer className="footer">
      <div className="shell">
        <div className="footer__grid">
          <div>
            <Logo />
            <p className="footer__about">{t('brand.tagline')}</p>
          </div>

          <div>
            <h4>{t('footer.sections')}</h4>
            <Link to="/reserver">{t('nav.flights')}</Link>
            <Link to="/nos-avions">{t('nav.fleet')}</Link>
            <Link to="/nos-services">{t('nav.services')}</Link>
            <Link to="/qui-sommes-nous">{t('nav.about')}</Link>
          </div>

          <div>
            <h4>{t('footer.book')}</h4>
            <Link to="/reserver">{t('nav.bookFlight')}</Link>
            <Link to="/devis">{t('nav.charter')}</Link>
            <Link to="/mes-reservations">{t('nav.myBookings')}</Link>
            <Link to="/contacts">{t('nav.contact')}</Link>
          </div>

          <div className="footer__contact">
            <h4>{t('footer.contactUs')}</h4>
            <a href="mailto:info@flymaluaviation.com">
              <Icon name="mail" size={16} />
              info@flymaluaviation.com
            </a>
            <a href="mailto:malu.avia@micronet.cd">
              <Icon name="mail" size={16} />
              malu.avia@micronet.cd
            </a>
            <a href="tel:+243990171122">
              <Icon name="phone" size={16} />
              +243 990 171 122
            </a>
            <Link to="/contacts">
              <Icon name="pin" size={16} />
              {t('footer.bases')}
            </Link>
          </div>
        </div>

        <div className="footer__bottom">
          <span>
            {year} &copy; Malu Aviation. {t('footer.rights')}
          </span>
          <span>{t('footer.since')}</span>
        </div>
      </div>
    </footer>
  );
}
