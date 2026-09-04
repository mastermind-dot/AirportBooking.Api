import { useTranslation } from 'react-i18next';

import PageHero from '../components/layout/PageHero';

export default function AboutPage() {
  const { t } = useTranslation();

  return (
    <>
      <PageHero eyebrow={t('home.eyebrow')} title={t('about.title')} lead={t('about.p1')} />

      <section className="section">
        <div className="shell">
          <div className="prose">
            <p>{t('about.p2')}</p>
            <p>{t('about.p3')}</p>
            <p>{t('about.p4')}</p>
            <p>{t('about.p5')}</p>
          </div>
        </div>
      </section>

      <section className="section section--tint">
        <div className="shell">
          <div className="section-head">
            <h2>{t('about.timelineTitle')}</h2>
          </div>

          <ul className="timeline" style={{ maxWidth: '62ch' }}>
            <li>
              <span className="timeline__year">1993</span>
              <span>{t('about.founded')}</span>
            </li>
            <li>
              <span className="timeline__year">1993</span>
              <span>{t('about.firstAircraft')}</span>
            </li>
            <li>
              <span className="timeline__year">{new Date().getFullYear()}</span>
              <span>{t('about.today')}</span>
            </li>
          </ul>
        </div>
      </section>
    </>
  );
}
