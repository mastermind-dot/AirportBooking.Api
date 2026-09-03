import { useEffect, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { flightsApi } from '../api/endpoints';
import { toProblem } from '../api/httpClient';
import type { Airport, FlightSummary, PagedResult } from '../types';
import { todayLocalIso } from '../utils/dates';

/**
 * The scheduled network only runs out of Goma, so the origin defaults there
 * rather than making every visitor pick it. Anything else the company flies is
 * charter, and the page says so.
 */
const DEFAULT_ORIGIN = 'GOM';

type SortBy = 'DepartureTime' | 'Price' | 'Duration';

export default function SearchPage() {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();

  const [airports, setAirports] = useState<Airport[]>([]);
  const [results, setResults] = useState<PagedResult<FlightSummary> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const origin = params.get('origin') ?? DEFAULT_ORIGIN;
  const destination = params.get('destination') ?? '';
  const date = params.get('date') ?? '';
  const passengers = Number(params.get('passengers') ?? 1);
  const sortBy = (params.get('sortBy') ?? 'DepartureTime') as SortBy;

  useEffect(() => {
    flightsApi.airports().then(setAirports).catch(() => setAirports([]));
  }, []);

  // Re-runs whenever the URL changes, so a shared or reloaded search link
  // reproduces the same results rather than an empty form.
  useEffect(() => {
    if (!destination || !date) {
      setResults(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    flightsApi
      .search({ origin, destination, departureDate: date, passengers, sortBy, pageSize: 20 })
      .then((data) => !cancelled && setResults(data))
      .catch((err) => !cancelled && setError(toProblem(err).detail ?? t('search.failed')))
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [origin, destination, date, passengers, sortBy, t]);

  // Controlled rather than defaultValue. The airport list arrives after the
  // first render, and an uncontrolled <select> that mounts with no options
  // silently falls back to whichever option lands first — which showed Beni as
  // the departure airport instead of Goma.
  const [form, setForm] = useState({ origin, destination, date, passengers });

  useEffect(() => {
    setForm({ origin, destination, date, passengers });
  }, [origin, destination, date, passengers]);

  const set = (field: keyof typeof form, value: string | number) =>
    setForm((current) => ({ ...current, [field]: value }));

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setParams({
      origin: form.origin,
      destination: form.destination,
      date: form.date,
      passengers: String(form.passengers),
      sortBy,
    });
  }

  const money = (amount: number, currency: string) =>
    new Intl.NumberFormat(i18n.resolvedLanguage, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);

  const clock = (isoLocal: string) =>
    new Intl.DateTimeFormat(i18n.resolvedLanguage, { hour: '2-digit', minute: '2-digit' })
      // The API already converted to airport-local time, so this must not shift
      // it again — parse as a plain wall clock.
      .format(new Date(isoLocal));

  const duration = (minutes: number) =>
    minutes >= 60 ? `${Math.floor(minutes / 60)} h ${minutes % 60 || ''}`.trim() : `${minutes} min`;

  return (
    <section className="section">
      <div className="shell">
        <div className="section-head">
          <p className="eyebrow">{t('search.eyebrow')}</p>
          <h2>{t('search.title')}</h2>
          <p className="lead">{t('search.lead')}</p>
        </div>

        <form className="search-bar" onSubmit={handleSubmit}>
          <label className="field">
            <span className="field__label">{t('search.from')}</span>
            <select value={form.origin} onChange={(e) => set('origin', e.target.value)}>
              {airports.map((a) => (
                <option key={a.iataCode} value={a.iataCode}>
                  {a.city} ({a.iataCode})
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span className="field__label">{t('search.to')}</span>
            <select
              value={form.destination}
              onChange={(e) => set('destination', e.target.value)}
              required
            >
              <option value="">{t('search.choose')}</option>
              {airports
                .filter((a) => a.iataCode !== form.origin)
                .map((a) => (
                  <option key={a.iataCode} value={a.iataCode}>
                    {a.city} ({a.iataCode})
                  </option>
                ))}
            </select>
          </label>

          <label className="field">
            <span className="field__label">{t('search.date')}</span>
            <input
              type="date"
              value={form.date}
              onChange={(e) => set('date', e.target.value)}
              min={todayLocalIso()}
              required
            />
          </label>

          <label className="field">
            <span className="field__label">{t('search.passengers')}</span>
            <input
              type="number"
              min="1"
              max="9"
              value={form.passengers}
              onChange={(e) => set('passengers', Number(e.target.value))}
            />
          </label>

          <button type="submit" className="btn btn--primary">
            {t('search.submit')}
          </button>
        </form>

        {loading && <p className="notice">{t('search.loading')}</p>}
        {error && <p className="notice notice--error">{error}</p>}

        {results && !loading && (
          <>
            <div className="results-head">
              <p>
                {results.totalCount === 0
                  ? t('search.noResults')
                  : t('search.resultCount', { count: results.totalCount })}
              </p>

              {results.totalCount > 0 && (
                <label className="field field--inline">
                  <span className="field__label">{t('search.sort')}</span>
                  <select
                    value={sortBy}
                    onChange={(e) => {
                      params.set('sortBy', e.target.value);
                      setParams(params);
                    }}
                  >
                    <option value="DepartureTime">{t('search.sortDeparture')}</option>
                    <option value="Price">{t('search.sortPrice')}</option>
                    <option value="Duration">{t('search.sortDuration')}</option>
                  </select>
                </label>
              )}
            </div>

            {results.totalCount === 0 && (
              <div className="notice">
                <p>{t('search.noResultsHelp')}</p>
                <Link to="/devis" className="btn btn--outline">
                  {t('search.askCharter')}
                </Link>
              </div>
            )}

            <div className="flight-list">
              {results.items.map((flight) => (
                <article key={flight.id} className="flight">
                  <div className="flight__times">
                    <div className="flight__endpoint">
                      <span className="flight__clock">{clock(flight.departureTimeLocal)}</span>
                      <span className="flight__iata">{flight.origin.iataCode}</span>
                    </div>

                    <div className="flight__leg">
                      <span className="flight__duration">{duration(flight.durationMinutes)}</span>
                      <span className="flight__line" aria-hidden="true" />
                      <span className="flight__direct">
                        {flight.isDirect ? t('search.direct') : t('search.stops', { count: flight.stops })}
                      </span>
                    </div>

                    <div className="flight__endpoint">
                      <span className="flight__clock">{clock(flight.arrivalTimeLocal)}</span>
                      <span className="flight__iata">{flight.destination.iataCode}</span>
                    </div>
                  </div>

                  <div className="flight__meta">
                    <span className="badge">{flight.flightNumber}</span>
                    <span>{flight.aircraftType}</span>
                    {flight.availableSeats <= 5 && (
                      <span className="badge badge--warn">
                        {t('search.seatsLeft', { count: flight.availableSeats })}
                      </span>
                    )}
                  </div>

                  <div className="flight__book">
                    <div className="flight__price">
                      <strong>{money(flight.pricePerPassenger, flight.currency)}</strong>
                      <span>{t('search.perPassenger')}</span>
                    </div>
                    <Link to={`/reserver/${flight.id}?passengers=${passengers}`} className="btn btn--primary">
                      {t('search.select')}
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          </>
        )}

        {!results && !loading && (
          <div className="notice">
            <p>{t('search.prompt')}</p>
          </div>
        )}
      </div>
    </section>
  );
}
