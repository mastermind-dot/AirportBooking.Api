import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Icon from '../components/ui/Icon';
import PageHero from '../components/layout/PageHero';
import FlightSearchCard from '../components/search/FlightSearchCard';
import { flightsApi } from '../api/endpoints';
import { toProblem } from '../api/httpClient';
import type { FlightSummary, PagedResult } from '../types';

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

  const [results, setResults] = useState<PagedResult<FlightSummary> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const origin = params.get('origin') ?? DEFAULT_ORIGIN;
  const destination = params.get('destination') ?? '';
  const date = params.get('date') ?? '';
  const passengers = Number(params.get('passengers') ?? 1);
  const sortBy = (params.get('sortBy') ?? 'DepartureTime') as SortBy;

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
    <>
      <PageHero eyebrow={t('search.eyebrow')} title={t('search.title')} lead={t('search.lead')} />

      <section className="section section--tight">
        <div className="shell">
          <div className="search-bar">
            <FlightSearchCard initial={{ origin, destination, date, passengers }} />
          </div>

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
                  <Link to="/devis" className="btn btn--outline btn--sm">
                    {t('search.askCharter')}
                    <Icon name="arrow-right" size={16} />
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
                          {flight.isDirect
                            ? t('search.direct')
                            : t('search.stops', { count: flight.stops })}
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
                      <Link
                        to={`/reserver/${flight.id}?passengers=${passengers}`}
                        className="btn btn--primary btn--sm"
                      >
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
    </>
  );
}
