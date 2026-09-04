import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Icon from '../ui/Icon';
import { flightsApi } from '../../api/endpoints';
import type { Airport } from '../../types';
import { todayLocalIso } from '../../utils/dates';

/**
 * The booking entry point: one card, used in three places — floating over the
 * hero, inside the header's search panel, and above the results on the search
 * page. All three submit to the same URL, so a search is always shareable and
 * the back button always works.
 *
 * The scheduled network only runs out of Goma, so the origin defaults there
 * rather than making every visitor pick it.
 */
const DEFAULT_ORIGIN = 'GOM';

const MAX_PASSENGERS = 9;

interface FlightSearchCardProps {
  variant?: 'floating' | 'flat';
  /** Pre-fills the card from an existing search. */
  initial?: { origin?: string; destination?: string; date?: string; passengers?: number };
  /** Lets the header panel close itself once a search is on its way. */
  onSubmitted?: () => void;
}

export default function FlightSearchCard({
  variant = 'floating',
  initial,
  onSubmitted,
}: FlightSearchCardProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [airports, setAirports] = useState<Airport[]>([]);
  const [form, setForm] = useState({
    origin: initial?.origin ?? DEFAULT_ORIGIN,
    destination: initial?.destination ?? '',
    date: initial?.date ?? '',
    passengers: initial?.passengers ?? 1,
  });

  // An unreachable API must not take the card down with it: the selects stay
  // empty, the rest of the page is unaffected, and a retry is a reload away.
  useEffect(() => {
    let cancelled = false;
    flightsApi
      .airports()
      .then((data) => !cancelled && setAirports(data))
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  // A shared or reloaded search link fills the card back in.
  useEffect(() => {
    if (!initial) return;
    setForm((current) => ({
      origin: initial.origin ?? current.origin,
      destination: initial.destination ?? current.destination,
      date: initial.date ?? current.date,
      passengers: initial.passengers ?? current.passengers,
    }));
  }, [initial?.origin, initial?.destination, initial?.date, initial?.passengers]);

  const set = (field: keyof typeof form, value: string | number) =>
    setForm((current) => ({ ...current, [field]: value }));

  function swap() {
    setForm((current) => ({ ...current, origin: current.destination, destination: current.origin }));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    navigate({
      pathname: '/reserver',
      search: new URLSearchParams({
        origin: form.origin,
        destination: form.destination,
        date: form.date,
        passengers: String(form.passengers),
      }).toString(),
    });

    onSubmitted?.();
  }

  const label = (airport: Airport) => `${airport.city} (${airport.iataCode})`;

  return (
    <form
      className={`searchcard${variant === 'flat' ? ' searchcard--flat' : ''}`}
      onSubmit={handleSubmit}
      aria-label={t('search.title')}
    >
      <label className="searchcard__field">
        <span className="searchcard__icon">
          <Icon name="pin" size={18} />
        </span>
        <span className="searchcard__label">{t('search.from')}</span>
        <select
          value={form.origin}
          onChange={(event) => set('origin', event.target.value)}
          className={form.origin ? undefined : 'is-empty'}
          required
        >
          <option value="">{t('search.fromPlaceholder')}</option>
          {airports.map((airport) => (
            <option key={airport.iataCode} value={airport.iataCode}>
              {label(airport)}
            </option>
          ))}
        </select>
      </label>

      <div className="searchcard__swap">
        <button type="button" onClick={swap} aria-label={t('search.swap')} title={t('search.swap')}>
          <Icon name="swap" size={17} />
        </button>
      </div>

      <label className="searchcard__field">
        <span className="searchcard__icon">
          <Icon name="pin" size={18} />
        </span>
        <span className="searchcard__label">{t('search.to')}</span>
        <select
          value={form.destination}
          onChange={(event) => set('destination', event.target.value)}
          className={form.destination ? undefined : 'is-empty'}
          required
        >
          <option value="">{t('search.toPlaceholder')}</option>
          {airports
            .filter((airport) => airport.iataCode !== form.origin)
            .map((airport) => (
              <option key={airport.iataCode} value={airport.iataCode}>
                {label(airport)}
              </option>
            ))}
        </select>
      </label>

      <label className="searchcard__field">
        <span className="searchcard__icon">
          <Icon name="calendar" size={18} />
        </span>
        <span className="searchcard__label">{t('search.dateLabel')}</span>
        <input
          type="date"
          value={form.date}
          onChange={(event) => set('date', event.target.value)}
          className={form.date ? undefined : 'is-empty'}
          min={todayLocalIso()}
          required
        />
      </label>

      <label className="searchcard__field">
        <span className="searchcard__icon">
          <Icon name="users" size={18} />
        </span>
        <span className="searchcard__label">{t('search.passengers')}</span>
        <select
          value={form.passengers}
          onChange={(event) => set('passengers', Number(event.target.value))}
        >
          {Array.from({ length: MAX_PASSENGERS }, (_, index) => index + 1).map((count) => (
            <option key={count} value={count}>
              {t('search.passengerCount', { count })}
            </option>
          ))}
        </select>
      </label>

      <div className="searchcard__submit">
        <button type="submit" className="btn btn--primary">
          {t('search.submit')}
          <Icon name="arrow-right" size={17} />
        </button>
      </div>
    </form>
  );
}
