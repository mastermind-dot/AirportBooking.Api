import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { FLEET } from '../data/fleet';

export default function FleetPage() {
  const { t, i18n } = useTranslation();
  const n = (value: number) => new Intl.NumberFormat(i18n.resolvedLanguage).format(value);

  return (
    <section className="section">
      <div className="shell">
        <div className="section-head">
          <p className="eyebrow">{t('brand.name')}</p>
          <h2>{t('fleet.title')}</h2>
          <p className="lead">{t('fleet.lead')}</p>
        </div>

        <div className="grid grid--2">
          {FLEET.map((aircraft) => (
            <article key={aircraft.id} className="card">
              <div className="card__media">
                <img src={aircraft.image} alt={aircraft.name} loading="lazy" />
              </div>

              <div className="card__body">
                <span className="badge">
                  {aircraft.count}&nbsp;
                  {aircraft.count > 1 ? t('fleet.unitMany') : t('fleet.unitOne')} ·{' '}
                  {t('fleet.basedAt')} {aircraft.base}
                </span>

                <h3 style={{ color: 'var(--brand-deep)' }}>{aircraft.name}</h3>

                <div className="spec-row">
                  <span>
                    <strong>{aircraft.passengers}</strong> {t('fleet.units.seats')}
                  </span>
                  <span>
                    <strong>{n(aircraft.cargoKg)}</strong> {t('fleet.units.kg')}
                  </span>
                  <span>
                    <strong>{aircraft.cruiseKmh}</strong> {t('fleet.units.kmh')}
                  </span>
                  <span>
                    <strong>{aircraft.enduranceHours}</strong> {t('fleet.units.hours')}
                  </span>
                </div>

                <div className="card__foot">
                  <Link to={`/nos-avions/${aircraft.id}`} className="btn btn--outline">
                    {t('fleet.viewDetails')}
                  </Link>
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
