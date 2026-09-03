import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

export default function Footer() {
  const { t } = useTranslation();
  const year = new Date().getFullYear();

  return (
    <footer className="footer">
      <div className="shell">
        <div className="footer__grid">
          <div>
            <div className="footer__logo">
              <img src="/images/logo.png" alt="Malu Aviation" />
            </div>
            <p style={{ marginTop: 16, maxWidth: '34ch' }}>{t('brand.tagline')}</p>
          </div>

          <div>
            <h4>{t('footer.sections')}</h4>
            <Link to="/qui-sommes-nous">{t('nav.about')}</Link>
            <Link to="/nos-avions">{t('nav.fleet')}</Link>
            <Link to="/nos-services">{t('nav.services')}</Link>
            <Link to="/contacts">{t('nav.contact')}</Link>
          </div>

          <div>
            <h4>{t('footer.contactUs')}</h4>
            <a href="mailto:info@flymaluaviation.com">info@flymaluaviation.com</a>
            <a href="mailto:malu.avia@micronet.cd">malu.avia@micronet.cd</a>
            <a href="tel:+243990171122">+243 990 171 122</a>
          </div>
        </div>

        <div className="footer__bottom">
          {year} &copy; Malu Aviation. {t('footer.rights')}
        </div>
      </div>
    </footer>
  );
}
