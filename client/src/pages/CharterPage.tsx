import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { chartersApi } from '../api/endpoints';
import { fieldErrors, toProblem } from '../api/httpClient';
import type { AircraftPreference, CharterKind, CharterRequestResult } from '../types';
import { todayLocalIso } from '../utils/dates';

const today = () => todayLocalIso();

export default function CharterPage() {
  const { t } = useTranslation();

  const [kind, setKind] = useState<CharterKind>('Passenger');
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<CharterRequestResult | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setErrors({});
    setFailure(null);

    const form = new FormData(event.currentTarget);
    const value = (name: string) => (form.get(name) as string)?.trim() || null;

    try {
      const created = await chartersApi.submit({
        kind,
        contactName: value('contactName') ?? '',
        contactEmail: value('contactEmail') ?? '',
        contactPhone: value('contactPhone'),
        company: value('company'),
        origin: value('origin') ?? '',
        destination: value('destination') ?? '',
        departureDate: value('departureDate') ?? '',
        returnDate: value('returnDate'),
        preferredAircraft: (value('preferredAircraft') ?? 'Any') as AircraftPreference,

        // Only the fields that belong to the chosen kind are sent. Sending both
        // would trip the server's rule that a cargo enquiry has no passenger
        // count, and vice versa.
        passengerCount: kind === 'Passenger' ? Number(form.get('passengerCount')) : null,
        cargoWeightKg: kind === 'Cargo' ? Number(form.get('cargoWeightKg')) : null,
        cargoDescription: kind === 'Cargo' ? value('cargoDescription') : null,
        message: value('message'),
      });

      setResult(created);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (error) {
      const problem = toProblem(error);
      const fields = fieldErrors(problem);

      if (Object.keys(fields).length > 0) {
        setErrors(fields);
      } else {
        setFailure(
          problem.status === 429 ? t('charter.tooMany') : problem.detail ?? t('charter.failed')
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <section className="section">
        <div className="shell">
          <div className="notice notice--success">
            <p className="eyebrow">{t('charter.sentTitle')}</p>
            <h2>{result.reference}</h2>
            <p className="lead">{t('charter.sentBody')}</p>
            <dl className="summary">
              <div>
                <dt>{t('charter.form.origin')}</dt>
                <dd>{result.origin}</dd>
              </div>
              <div>
                <dt>{t('charter.form.destination')}</dt>
                <dd>{result.destination}</dd>
              </div>
              <div>
                <dt>{t('charter.form.departureDate')}</dt>
                <dd>{result.departureDate}</dd>
              </div>
            </dl>
            <button type="button" className="btn btn--outline" onClick={() => setResult(null)}>
              {t('charter.another')}
            </button>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="section">
      <div className="shell">
        <div className="section-head">
          <p className="eyebrow">{t('brand.name')}</p>
          <h2>{t('charter.title')}</h2>
          <p className="lead">{t('charter.lead')}</p>
        </div>

        <form className="form" onSubmit={handleSubmit} noValidate>
          <fieldset className="field-group">
            <legend>{t('charter.form.kind')}</legend>
            <div className="choice-row">
              {(['Passenger', 'Cargo'] as const).map((option) => (
                <label key={option} className={`choice ${kind === option ? 'is-selected' : ''}`}>
                  <input
                    type="radio"
                    name="kind"
                    value={option}
                    checked={kind === option}
                    onChange={() => setKind(option)}
                  />
                  {t(`charter.kinds.${option}`)}
                </label>
              ))}
            </div>
          </fieldset>

          <div className="field-grid">
            <Field name="contactName" label={t('charter.form.name')} required error={errors.contactName} />
            <Field name="contactEmail" label={t('charter.form.email')} type="email" required error={errors.contactEmail} />
            <Field name="contactPhone" label={t('charter.form.phone')} error={errors.contactPhone} />
            <Field name="company" label={t('charter.form.company')} error={errors.company} />
          </div>

          <div className="field-grid">
            <Field
              name="origin"
              label={t('charter.form.origin')}
              hint={t('charter.form.originHint')}
              required
              error={errors.origin}
            />
            <Field
              name="destination"
              label={t('charter.form.destination')}
              hint={t('charter.form.destinationHint')}
              required
              error={errors.destination}
            />
            <Field
              name="departureDate"
              label={t('charter.form.departureDate')}
              type="date"
              min={today()}
              required
              error={errors.departureDate}
            />
            <Field
              name="returnDate"
              label={t('charter.form.returnDate')}
              type="date"
              min={today()}
              error={errors.returnDate}
            />
          </div>

          <div className="field-grid">
            <label className="field">
              <span className="field__label">{t('charter.form.aircraft')}</span>
              <select name="preferredAircraft" defaultValue="Any">
                <option value="Any">{t('charter.aircraft.Any')}</option>
                <option value="ShortSd360">{t('charter.aircraft.ShortSd360')}</option>
                <option value="GulfstreamG159">{t('charter.aircraft.GulfstreamG159')}</option>
              </select>
            </label>

            {kind === 'Passenger' ? (
              <Field
                name="passengerCount"
                label={t('charter.form.passengers')}
                type="number"
                min="1"
                max="30"
                defaultValue="1"
                required
                error={errors.passengerCount}
              />
            ) : (
              <Field
                name="cargoWeightKg"
                label={t('charter.form.cargoWeight')}
                hint={t('charter.form.cargoWeightHint')}
                type="number"
                min="1"
                max="3500"
                required
                error={errors.cargoWeightKg}
              />
            )}
          </div>

          {kind === 'Cargo' && (
            <Field
              name="cargoDescription"
              label={t('charter.form.cargoDescription')}
              required
              error={errors.cargoDescription}
            />
          )}

          <label className="field">
            <span className="field__label">{t('charter.form.message')}</span>
            <textarea name="message" rows={4} maxLength={2000} />
            {errors.message && <span className="field__error">{errors.message}</span>}
          </label>

          {failure && <p className="notice notice--error">{failure}</p>}

          <button type="submit" className="btn btn--primary" disabled={submitting}>
            {submitting ? t('charter.sending') : t('charter.submit')}
          </button>

          <p className="form__note">{t('charter.noPriceNote')}</p>
        </form>
      </div>
    </section>
  );
}

interface FieldProps {
  name: string;
  label: string;
  type?: string;
  hint?: string;
  error?: string;
  required?: boolean;
  min?: string;
  max?: string;
  defaultValue?: string;
}

function Field({ name, label, type = 'text', hint, error, ...rest }: FieldProps) {
  return (
    <label className="field">
      <span className="field__label">
        {label}
        {rest.required && <span aria-hidden="true"> *</span>}
      </span>
      {hint && <span className="field__hint">{hint}</span>}
      <input
        id={name}
        name={name}
        type={type}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${name}-error` : undefined}
        {...rest}
      />
      {error && (
        <span className="field__error" id={`${name}-error`}>
          {error}
        </span>
      )}
    </label>
  );
}
