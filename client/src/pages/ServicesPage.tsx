import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const SERVICES = ['passengerSd360', 'passengerG159', 'cargo'] as const;

export default function ServicesPage() {
  const { t } = useTranslation();

  return (
    <section className="section">
      <div className="shell">
        <div className="section-head">
          <p className="eyebrow">{t('brand.name')}</p>
          <h2>{t('services.title')}</h2>
          <p className="lead">{t('services.lead')}</p>
        </div>

        <div className="grid grid--3">
          {SERVICES.map((key) => (
            <article key={key} className="card">
              <div className="card__body">
                <h3 style={{ color: 'var(--brand-deep)' }}>{t(`services.${key}.name`)}</h3>
                <p>{t(`services.${key}.body`)}</p>
              </div>
            </article>
          ))}
        </div>

        <div style={{ marginTop: 40 }}>
          <Link to="/contacts" className="btn btn--primary">
            {t('home.ctaContact')}
          </Link>
        </div>
      </div>
    </section>
  );
}
