import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { bookingsApi } from '../api/endpoints';
import { useAuthStore } from '../store/authStore';
import type { BookingSummary } from '../types';

export default function MyBookingsPage() {
  const { t, i18n } = useTranslation();
  const { accessToken, bootstrapped } = useAuthStore();
  const [items, setItems] = useState<BookingSummary[] | null>(null);

  useEffect(() => {
    if (accessToken) {
      bookingsApi.mine().then((r) => setItems(r.items)).catch(() => setItems([]));
    }
  }, [accessToken]);

  if (!bootstrapped) return null;
  if (!accessToken) return <Navigate to="/connexion" replace state={{ from: '/mes-reservations' }} />;

  const money = (amount: number, currency: string) =>
    new Intl.NumberFormat(i18n.resolvedLanguage, { style: 'currency', currency, maximumFractionDigits: 0 })
      .format(amount);

  const day = (iso: string) =>
    new Intl.DateTimeFormat(i18n.resolvedLanguage, { dateStyle: 'medium' }).format(new Date(iso));

  return (
    <section className="section">
      <div className="shell">
        <div className="section-head">
          <h2>{t('nav.myBookings')}</h2>
        </div>

        {items === null && <p className="notice">…</p>}

        {items?.length === 0 && (
          <div className="notice">
            <p>{t('search.prompt')}</p>
            <Link to="/reserver" className="btn btn--outline">{t('nav.book')}</Link>
          </div>
        )}

        <div className="flight-list">
          {items?.map((booking) => (
            <article key={booking.id} className="flight">
              <div>
                <span className="badge">{booking.reference}</span>
                <h3 style={{ marginTop: 10, color: 'var(--brand-deep)' }}>
                  {booking.flight.originCity} → {booking.flight.destinationCity}
                </h3>
                <p style={{ color: 'var(--muted)', margin: '6px 0 0' }}>
                  {booking.flight.flightNumber} · {day(booking.flight.departureTimeLocal)} ·{' '}
                  {booking.passengerCount} × {money(booking.totalAmount / Math.max(booking.passengerCount, 1), booking.currency)}
                </p>
              </div>

              <div className="flight__book">
                <span className={`status status--${booking.status.toLowerCase()}`}>
                  {t(`booking.status.${booking.status}`)}
                </span>
                <strong>{money(booking.totalAmount, booking.currency)}</strong>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
