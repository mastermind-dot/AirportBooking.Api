import { useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { loadStripe } from '@stripe/stripe-js';
import {
  Elements,
  PaymentElement,
  useElements,
  useStripe,
} from '@stripe/react-stripe-js';

import { bookingsApi, http } from '../api/endpoints';
import { toProblem } from '../api/httpClient';
import { useAuthStore } from '../store/authStore';
import type { Booking } from '../types';

/**
 * Loaded once at module scope, not per render — loadStripe injects a script
 * tag, and calling it on every render would add one each time.
 *
 * The publishable key is meant to be here. It can only confirm payments the
 * server has already authorised by creating the intent; it cannot move money on
 * its own, which is why it ships in the bundle and the secret key never does.
 */
const stripePromise = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY
  ? loadStripe(import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY)
  : null;

export default function PaymentPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const { accessToken, bootstrapped } = useAuthStore();

  const [booking, setBooking] = useState<Booking | null>(null);
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id || !accessToken) return;

    let cancelled = false;

    (async () => {
      try {
        const found = await bookingsApi.byId(id);
        if (cancelled) return;
        setBooking(found);

        // The amount is not sent — the server reads it from the booking. There
        // is no field here a tampered request could change.
        const intent = await http
          .post<{ clientSecret: string }>('/payments/create-intent', { bookingId: id })
          .then((r) => r.data);

        if (!cancelled) setClientSecret(intent.clientSecret);
      } catch (err) {
        if (cancelled) return;
        const problem = toProblem(err);
        setError(
          problem.status === 503 ? t('payment.unavailable') : problem.detail ?? t('payment.failed')
        );
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [id, accessToken, t]);

  if (!bootstrapped) return null;
  if (!accessToken) return <Navigate to="/connexion" replace state={{ from: `/paiement/${id}` }} />;

  if (!stripePromise) {
    return (
      <section className="section">
        <div className="shell shell--narrow">
          <p className="notice notice--error">{t('payment.noKey')}</p>
        </div>
      </section>
    );
  }

  return (
    <section className="section">
      <div className="shell shell--narrow">
        <div className="section-head">
          <h2>{t('payment.title')}</h2>
          {booking && (
            <p className="lead">
              {booking.reference} · {booking.flight.originIata} → {booking.flight.destinationIata} ·{' '}
              <strong>
                {new Intl.NumberFormat(undefined, {
                  style: 'currency',
                  currency: booking.currency,
                  maximumFractionDigits: 0,
                }).format(booking.totalAmount)}
              </strong>
            </p>
          )}
        </div>

        {error && <p className="notice notice--error">{error}</p>}

        {clientSecret && (
          <Elements
            stripe={stripePromise}
            options={{
              clientSecret,
              appearance: {
                theme: 'flat',
                variables: { colorPrimary: '#01489e', borderRadius: '4px' },
              },
            }}
          >
            <CheckoutForm bookingId={id!} />
          </Elements>
        )}

        {!clientSecret && !error && <p className="notice">{t('payment.preparing')}</p>}
      </div>
    </section>
  );
}

function CheckoutForm({ bookingId }: { bookingId: string }) {
  const stripe = useStripe();
  const elements = useElements();
  const { t } = useTranslation();

  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!stripe || !elements) return;

    setBusy(true);
    setMessage(null);

    const { error } = await stripe.confirmPayment({
      elements,
      confirmParams: {
        // Unused as things stand: the intent is created with
        // allow_redirects "never", so no offered method leaves the page. Kept
        // deliberately, so flipping that server-side setting back cannot break
        // confirmation here.
        return_url: `${window.location.origin}/paiement/${bookingId}`,
      },
      redirect: 'if_required',
    });

    if (error) {
      setMessage(error.message ?? t('payment.declined'));
      setBusy(false);
      return;
    }

    // Stripe has taken the money, but this booking is not confirmed until the
    // webhook arrives and the server verifies its signature. The card result is
    // for the user's benefit; the webhook is the source of truth. So poll for
    // the status rather than claiming success the browser cannot vouch for.
    setConfirming(true);
    setBusy(false);

    for (let attempt = 0; attempt < 10; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, 2000));

      try {
        const booking = await bookingsApi.byId(bookingId);
        if (booking.status === 'Confirmed') {
          setConfirmed(true);
          setConfirming(false);
          return;
        }
        if (booking.status === 'Failed') {
          setMessage(t('payment.declined'));
          setConfirming(false);
          return;
        }
      } catch {
        // Keep polling; a transient failure here is not a payment failure.
      }
    }

    // Payment succeeded but the webhook has not landed yet. Say exactly that
    // rather than implying anything went wrong.
    setConfirming(false);
    setMessage(t('payment.slowWebhook'));
  }

  if (confirmed) {
    return (
      <div className="notice notice--success">
        <p className="eyebrow">{t('payment.confirmedTitle')}</p>
        <p className="lead">{t('payment.confirmedBody')}</p>
        <Link to="/mes-reservations" className="btn btn--outline">
          {t('nav.myBookings')}
        </Link>
      </div>
    );
  }

  return (
    <form className="form" onSubmit={handleSubmit}>
      <PaymentElement />

      {message && <p className="notice notice--error">{message}</p>}
      {confirming && <p className="notice">{t('payment.confirming')}</p>}

      <button type="submit" className="btn btn--primary" disabled={!stripe || busy || confirming}>
        {busy ? t('payment.processing') : t('payment.pay')}
      </button>

      <p className="form__note">{t('payment.secureNote')}</p>
    </form>
  );
}
