import { useTranslation } from 'react-i18next';

import PageHero from '../components/layout/PageHero';

/**
 * Contact details as published on the company's own site. Kept as structured
 * data rather than hand-written markup so the two bases stay consistent and a
 * changed number is a one-line edit.
 */
const BASES = [
  {
    airport: 'AÉROPORT DE NDOLO / KINSHASA',
    people: [
      { roleKey: 'dirigeant', phone: '+243 990 171 122', email: 'kasindifranc@flymaluaviation.com' },
      { roleKey: 'commercial', phone: '+243 999 932 155', email: 'bokungudidier@flymaluaviation.com' },
      { roleKey: 'operations', phone: '+243 818 846 801', email: 'operationkinshasa@flymaluaviation.com' },
    ],
  },
  {
    airport: 'AÉROPORT DE GOMA',
    people: [
      { roleKey: 'operationsAir', phone: '+243 994 387 507', email: 'marionjj@flymaluaviation.com' },
      { roleKey: 'commercialResp', phone: '+243 998 666 515', email: 'kyombapaul@flymaluaviation.com' },
      { roleKey: 'operations', phone: '+243 998 766 895', email: 'operationgoma@flymaluaviation.com' },
    ],
  },
];

const GENERAL_EMAILS = ['malu.avia@micronet.cd', 'info@flymaluaviation.com'];

/** Strips spaces so tel: links dial correctly on mobile. */
const dial = (phone: string) => phone.replace(/\s/g, '');

export default function ContactPage() {
  const { t } = useTranslation();

  return (
    <>
      <PageHero eyebrow={t('brand.name')} title={t('contact.title')} lead={t('contact.lead')} />

      <section className="section">
        <div className="shell">
          <div className="grid grid--2">
            {BASES.map((base) => (
              <div key={base.airport} className="contact-card">
                <h3>{base.airport}</h3>

                {base.people.map((person) => (
                  <div key={person.email} className="person">
                    <div className="person__role">{t(`contact.roles.${person.roleKey}`)}</div>
                    <div className="person__lines">
                      <a href={`tel:${dial(person.phone)}`}>{person.phone}</a>
                      <a href={`mailto:${person.email}`}>{person.email}</a>
                    </div>
                  </div>
                ))}
              </div>
            ))}
          </div>

          <div className="contact-card" style={{ marginTop: 26 }}>
            <h3>{t('contact.generalTitle')}</h3>
            <div className="person__lines">
              {GENERAL_EMAILS.map((email) => (
                <a key={email} href={`mailto:${email}`}>
                  {email}
                </a>
              ))}
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
