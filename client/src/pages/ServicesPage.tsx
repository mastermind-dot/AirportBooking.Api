import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import PageHero from '../components/layout/PageHero';

const SERVICES = ['passengerSd360', 'passengerG159', 'cargo'] as const;

export default function ServicesPage() {
  const { t } = useTranslation();

  return (
    <>
      <PageHero eyebrow={t('brand.name')} title={t('services.title')} lead={t('services.lead')} />

      <section className="section">
        <div className="shell">
          <div className="grid grid--3">
            {SERVICES.map((key) => (
              <article key={key} className="card">
                <div className="card__body">
                  <h3>{t(`services.${key}.name`)}</h3>
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
    </>
  );
}
