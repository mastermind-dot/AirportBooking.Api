import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { FLEET } from '../data/fleet';

export default function HomePage() {
  const { t, i18n } = useTranslation();

  // Formatted for the active locale: French readers expect "1 800", English
  // readers "1,800". Hardcoding either is wrong for half the audience.
  const n = (value: number) => new Intl.NumberFormat(i18n.resolvedLanguage).format(value);

  const yearsOperating = new Date().getFullYear() - 1993;

  return (
    <>
      <section className="hero">
        <div className="hero__media">
          <img src="/images/hero-charter.jpg" alt="" aria-hidden="true" />
        </div>

        <div className="shell hero__body">
          <span className="hero__eyebrow">{t('home.eyebrow')}</span>
          <h1>{t('home.heroTitle')}</h1>
          <p className="hero__lead">{t('home.heroLead')}</p>

          <div className="hero__cta">
            <Link to="/nos-avions" className="btn btn--primary">
              {t('home.ctaFleet')}
            </Link>
            <Link to="/contacts" className="btn btn--ghost">
              {t('home.ctaContact')}
            </Link>
          </div>
        </div>
      </section>

      <section className="stats" aria-label={t('brand.tagline')}>
        <div className="stat">
          <div className="stat__value">{n(yearsOperating)}</div>
          <div className="stat__label">{t('home.statsYears')}</div>
        </div>
        <div className="stat">
          <div className="stat__value">3</div>
          <div className="stat__label">{t('home.statsAircraft')}</div>
        </div>
        <div className="stat">
          <div className="stat__value">2</div>
          <div className="stat__label">{t('home.statsBases')}</div>
        </div>
        <div className="stat">
          <div className="stat__value">30</div>
          <div className="stat__label">{t('home.statsSeats')}</div>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="section-head">
            <p className="eyebrow">{t('nav.about')}</p>
            <h2>{t('home.aboutTitle')}</h2>
            <p className="lead">{t('home.aboutLead')}</p>
          </div>
          <Link to="/qui-sommes-nous" className="btn btn--outline">
            {t('home.aboutMore')}
          </Link>
        </div>
      </section>

      <section className="section section--tint">
        <div className="shell">
          <div className="section-head">
            <p className="eyebrow">{t('home.servicesTitle')}</p>
            <h2>{t('home.servicesLead')}</h2>
          </div>

          <div className="grid grid--3">
            {(['passengerSd360', 'passengerG159', 'cargo'] as const).map((key) => (
              <article key={key} className="card">
                <div className="card__body">
                  <h3 style={{ color: 'var(--brand-deep)' }}>{t(`services.${key}.name`)}</h3>
                  <p>{t(`services.${key}.body`)}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="section-head">
            <p className="eyebrow">{t('home.fleetTitle')}</p>
            <h2>{t('home.fleetLead')}</h2>
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

      <section className="section section--brand">
        <div className="shell">
          <p className="eyebrow">AAC · OACI / ICAO</p>
          <h2>{t('home.safetyTitle')}</h2>
          <p className="lead">{t('home.safetyBody')}</p>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="section-head">
            <p className="eyebrow">{t('nav.contact')}</p>
            <h2>{t('home.contactTitle')}</h2>
            <p className="lead">{t('home.contactLead')}</p>
          </div>
          <Link to="/contacts" className="btn btn--primary">
            {t('home.ctaContact')}
          </Link>
        </div>
      </section>
    </>
  );
}
