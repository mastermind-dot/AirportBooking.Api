import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Icon, { type IconName } from '../components/ui/Icon';
import FlightSearchCard from '../components/search/FlightSearchCard';
import { FLEET } from '../data/fleet';

const FEATURES: { key: string; icon: IconName }[] = [
  { key: 'safety', icon: 'shield' },
  { key: 'service', icon: 'star' },
  { key: 'flexible', icon: 'clock' },
  { key: 'reach', icon: 'plane' },
];

const SERVICES = ['passengerSd360', 'passengerG159', 'cargo'] as const;

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
            <Link to="/reserver" className="btn btn--primary">
              {t('nav.bookFlight')}
              <Icon name="arrow-right" size={17} />
            </Link>
            <Link to="/nos-avions" className="btn btn--ghost">
              {t('home.ctaFleet')}
              <Icon name="arrow-right" size={17} />
            </Link>
          </div>
        </div>
      </section>

      {/* Lifted into the hero's bottom edge: booking is the first thing on the
          page, not something to scroll for. */}
      <div className="shell searchcard-wrap">
        <FlightSearchCard />
      </div>

      <section className="section--tight">
        <div className="shell">
          <div className="features">
            {FEATURES.map((feature) => (
              <div key={feature.key} className="feature">
                <span className="feature__icon">
                  <Icon name={feature.icon} size={21} />
                </span>
                <div>
                  <h3 className="feature__title">{t(`home.features.${feature.key}.title`)}</h3>
                  <p className="feature__body">{t(`home.features.${feature.key}.body`)}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="split">
        <div className="split__media">
          <img src="/images/hero-cargo.jpg" alt={t('home.splitCaption')} loading="lazy" />
          <span className="split__caption">{t('home.splitCaption')}</span>
        </div>

        <div className="split__body">
          <p className="eyebrow">{t('home.servicesTitle')}</p>
          <h2>{t('home.servicesLead')}</h2>
          <p className="lead">{t('home.servicesBody')}</p>

          <ul className="checks" style={{ margin: '28px 0 32px' }}>
            {SERVICES.map((key) => (
              <li key={key}>
                <Icon name="check" size={18} strokeWidth={2.4} />
                <span>{t(`services.${key}.name`)}</span>
              </li>
            ))}
          </ul>

          <div className="hero__cta">
            <Link to="/nos-services" className="btn btn--primary">
              {t('home.servicesCta')}
              <Icon name="arrow-right" size={17} />
            </Link>
            <Link to="/devis" className="btn btn--outline">
              {t('nav.charter')}
            </Link>
          </div>
        </div>
      </section>

      <section className="section section--tint">
        <div className="shell">
          <div className="section-head section-head--center">
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
                  <h3>{aircraft.name}</h3>

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
                    <Link to={`/nos-avions/${aircraft.id}`} className="btn btn--outline btn--sm">
                      {t('fleet.viewDetails')}
                      <Icon name="arrow-right" size={16} />
                    </Link>
                  </div>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="stats">
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
          </div>
        </div>
      </section>

      <section className="split split--flip section--brand">
        <div className="split__media">
          <img src="/images/sd360.png" alt="" aria-hidden="true" loading="lazy" />
          <span className="split__caption">{t('home.safetyCaption')}</span>
        </div>

        <div className="split__body">
          <p className="eyebrow">AAC &middot; OACI / ICAO</p>
          <h2>{t('home.safetyTitle')}</h2>
          <p className="lead">{t('home.safetyBody')}</p>

          <div className="hero__cta" style={{ marginTop: 32 }}>
            <Link to="/qui-sommes-nous" className="btn btn--ghost">
              {t('home.aboutMore')}
              <Icon name="arrow-right" size={17} />
            </Link>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="cta-band">
            <div>
              <h2>{t('home.contactTitle')}</h2>
              <p className="lead">{t('home.contactLead')}</p>
            </div>

            <div className="cta-band__actions">
              <Link to="/devis" className="btn btn--primary">
                {t('home.ctaContact')}
                <Icon name="arrow-right" size={17} />
              </Link>
              <Link to="/contacts" className="btn btn--ghost">
                {t('nav.contact')}
              </Link>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
