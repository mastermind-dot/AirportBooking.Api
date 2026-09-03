import { Link, useParams, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { findAircraft } from '../data/fleet';

export default function AircraftPage() {
  const { id } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation();

  const aircraft = id ? findAircraft(id) : undefined;

  // An unknown id is a bad URL, not an error page worth designing: send the
  // visitor to the fleet list, which is what they were looking for.
  if (!aircraft) {
    return <Navigate to="/nos-avions" replace />;
  }

  const n = (value: number) => new Intl.NumberFormat(i18n.resolvedLanguage).format(value);

  const specs = [
    { label: t('fleet.specs.passengers'), value: n(aircraft.passengers), unit: t('fleet.units.seats') },
    { label: t('fleet.specs.cargo'), value: n(aircraft.cargoKg), unit: t('fleet.units.kg') },
    { label: t('fleet.specs.cruise'), value: n(aircraft.cruiseKmh), unit: t('fleet.units.kmh') },
    { label: t('fleet.specs.endurance'), value: n(aircraft.enduranceHours), unit: t('fleet.units.hours') },
    ...(aircraft.rangeKm
      ? [{ label: t('fleet.specs.range'), value: n(aircraft.rangeKm), unit: t('fleet.units.km') }]
      : []),
    ...(aircraft.cargoVolumeM3
      ? [{ label: t('fleet.specs.volume'), value: n(aircraft.cargoVolumeM3), unit: t('fleet.units.m3') }]
      : []),
    { label: t('fleet.specs.base'), value: aircraft.base, unit: '' },
  ];

  return (
    <>
      <section className="detail-hero">
        <div className="shell">
          <Link to="/nos-avions" className="back-link">
            &larr; {t('fleet.backToFleet')}
          </Link>

          <p className="eyebrow" style={{ color: 'var(--accent)' }}>
            {aircraft.count} {aircraft.count > 1 ? t('fleet.unitMany') : t('fleet.unitOne')} ·{' '}
            {t('fleet.inService')}
          </p>
          <h1>{aircraft.name}</h1>

          <div className="detail-hero__media">
            <img src={aircraft.image} alt={aircraft.name} />
          </div>
        </div>
      </section>

      <section className="section">
        <div className="shell">
          <div className="section-head">
            <h2>{t('fleet.specs.title')}</h2>
          </div>

          <div className="specs">
            {specs.map((spec) => (
              <div key={spec.label} className="spec">
                <div className="spec__label">{spec.label}</div>
                <div className="spec__value">
                  {spec.value}
                  {spec.unit && <span className="spec__unit">{spec.unit}</span>}
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="section section--tint">
        <div className="shell">
          <div className="section-head">
            <h2>{t('fleet.highlights.title')}</h2>
          </div>

          <ul className="checks" style={{ maxWidth: '60ch' }}>
            {aircraft.highlightKeys.map((key) => (
              <li key={key}>
                <span>{t(`fleet.highlights.${key}`)}</span>
              </li>
            ))}
          </ul>

          <div style={{ marginTop: 36 }}>
            <Link to="/contacts" className="btn btn--primary">
              {t('fleet.cta')}
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}
