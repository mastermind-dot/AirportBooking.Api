import { useEffect, useState, type FormEvent } from 'react';
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { bookingsApi, flightsApi } from '../api/endpoints';
import { fieldErrors, toProblem } from '../api/httpClient';
import { useAuthStore } from '../store/authStore';
import type { Booking, PassengerInput } from '../types';
import { todayLocalIso } from '../utils/dates';

interface FlightDetails {
  id: string;
  flightNumber: string;
  aircraftType: string;
  origin: { iataCode: string; city: string };
  destination: { iataCode: string; city: string };
  departureTimeLocal: string;
  arrivalTimeLocal: string;
  currency: string;
  availableSeats: number;
  fare: number;
}

const emptyPassenger = (): PassengerInput => ({
  firstName: '',
  lastName: '',
  dateOfBirth: '',
  nationality: '',
  passportNumber: '',
  passportExpiry: '',
});

export default function BookingPage() {
  const { id } = useParams<{ id: string }>();
  const [params] = useSearchParams();
  const { t, i18n } = useTranslation();

  const { accessToken, user, bootstrapped } = useAuthStore();

  const count = Math.min(Math.max(Number(params.get('passengers') ?? 1), 1), 9);
  const [flight, setFlight] = useState<FlightDetails | null>(null);
  const [passengers, setPassengers] = useState<PassengerInput[]>(
    Array.from({ length: count }, emptyPassenger)
  );
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [created, setCreated] = useState<Booking | null>(null);

  useEffect(() => {
    if (id) {
      flightsApi.byId(id).then(setFlight).catch(() => setFlight(null));
    }
  }, [id]);

  // Wait for the refresh attempt to settle before deciding — otherwise a
  // signed-in user reloading this page is bounced to the sign-in form.
  if (!bootstrapped) {
    return <section className="section"><div className="shell"><p className="notice">…</p></div></section>;
  }

  if (!accessToken) {
    return <Navigate to="/connexion" replace state={{ from: `/reserver/${id}?passengers=${count}` }} />;
  }

  function update(index: number, field: keyof PassengerInput, value: string) {
    setPassengers((current) =>
      current.map((p, i) => (i === index ? { ...p, [field]: value } : p))
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!id) return;

    setBusy(true);
    setErrors({});
    setFailure(null);

    const form = new FormData(event.currentTarget);

    try {
      const booking = await bookingsApi.create({
        flightId: id,
        contactEmail: String(form.get('contactEmail') ?? '').trim(),
        contactPhone: String(form.get('contactPhone') ?? '').trim() || null,
        passengers: passengers.map((p) => ({
          ...p,
          passportExpiry: p.passportExpiry || null,
        })),
      });

      setCreated(booking);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (error) {
      const problem = toProblem(error);
      const fields = fieldErrors(problem);

      if (Object.keys(fields).length > 0) {
        setErrors(fields);
      } else {
        setFailure(problem.detail ?? t('booking.failed'));
      }
    } finally {
      setBusy(false);
    }
  }

  const money = (amount: number, currency: string) =>
    new Intl.NumberFormat(i18n.resolvedLanguage, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);

  if (created) {
    return (
      <section className="section">
        <div className="shell shell--narrow">
          <div className="notice notice--success">
            <p className="eyebrow">{t('booking.createdTitle')}</p>
            <h2>{created.reference}</h2>
            <p className="lead">{t('booking.createdBody')}</p>
            <dl className="summary">
              <div>
                <dt>{created.flight.originIata} → {created.flight.destinationIata}</dt>
                <dd>{created.flight.flightNumber}</dd>
              </div>
              <div>
                <dt>{t('booking.total')}</dt>
                <dd>{money(created.totalAmount, created.currency)}</dd>
              </div>
            </dl>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
              <Link to={`/paiement/${created.id}`} className="btn btn--primary">
                {t('payment.pay')}
              </Link>
              <Link to="/mes-reservations" className="btn btn--outline">
                {t('nav.myBookings')}
              </Link>
            </div>
          </div>
        </div>
      </section>
    );
  }

  const fare = flight?.fare ?? 0;

  return (
    <section className="section">
      <div className="shell shell--narrow">
        <div className="section-head">
          <h2>{t('booking.title')}</h2>
          <p className="lead">{t('booking.lead')}</p>
        </div>

        {flight && (
          <div className="notice">
            <strong>
              {flight.origin.city} ({flight.origin.iataCode}) → {flight.destination.city} (
              {flight.destination.iataCode})
            </strong>
            <p style={{ margin: '6px 0 0' }}>
              {flight.flightNumber} · {flight.aircraftType} ·{' '}
              {money(fare, flight.currency)} × {count} ={' '}
              <strong>{money(fare * count, flight.currency)}</strong>
            </p>
          </div>
        )}

        <form className="form" onSubmit={handleSubmit} noValidate>
          {passengers.map((passenger, index) => (
            <fieldset key={index} className="field-group">
              <legend>{t('booking.passenger', { n: index + 1 })}</legend>

              <div className="field-grid">
                <label className="field">
                  <span className="field__label">{t('booking.firstName')}</span>
                  <input
                    value={passenger.firstName}
                    onChange={(e) => update(index, 'firstName', e.target.value)}
                    required
                  />
                  {errors[`passengers[${index}].firstName`] && (
                    <span className="field__error">{errors[`passengers[${index}].firstName`]}</span>
                  )}
                </label>

                <label className="field">
                  <span className="field__label">{t('booking.lastName')}</span>
                  <input
                    value={passenger.lastName}
                    onChange={(e) => update(index, 'lastName', e.target.value)}
                    required
                  />
                </label>

                <label className="field">
                  <span className="field__label">{t('booking.dateOfBirth')}</span>
                  <input
                    type="date"
                    max={todayLocalIso()}
                    value={passenger.dateOfBirth}
                    onChange={(e) => update(index, 'dateOfBirth', e.target.value)}
                    required
                  />
                </label>

                <label className="field">
                  <span className="field__label">{t('booking.nationality')}</span>
                  <input
                    maxLength={2}
                    style={{ textTransform: 'uppercase' }}
                    value={passenger.nationality}
                    onChange={(e) => update(index, 'nationality', e.target.value.toUpperCase())}
                    required
                  />
                </label>

                <label className="field">
                  <span className="field__label">{t('booking.passportNumber')}</span>
                  <input
                    value={passenger.passportNumber}
                    onChange={(e) => update(index, 'passportNumber', e.target.value.toUpperCase())}
                    required
                  />
                </label>

                <label className="field">
                  <span className="field__label">{t('booking.passportExpiry')}</span>
                  <input
                    type="date"
                    value={passenger.passportExpiry ?? ''}
                    onChange={(e) => update(index, 'passportExpiry', e.target.value)}
                  />
                </label>
              </div>
            </fieldset>
          ))}

          <fieldset className="field-group">
            <legend>{t('booking.contactEmail')}</legend>
            <div className="field-grid">
              <label className="field">
                <span className="field__label">{t('booking.contactEmail')}</span>
                <input name="contactEmail" type="email" defaultValue={user?.email ?? ''} required />
                {errors.contactEmail && <span className="field__error">{errors.contactEmail}</span>}
              </label>
              <label className="field">
                <span className="field__label">{t('booking.contactPhone')}</span>
                <input name="contactPhone" />
                {errors.contactPhone && <span className="field__error">{errors.contactPhone}</span>}
              </label>
            </div>
          </fieldset>

          {failure && <p className="notice notice--error">{failure}</p>}

          <button type="submit" className="btn btn--primary" disabled={busy}>
            {busy ? t('booking.submitting') : t('booking.submit')}
          </button>
        </form>
      </div>
    </section>
  );
}
