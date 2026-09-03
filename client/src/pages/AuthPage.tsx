import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { authApi } from '../api/endpoints';
import { fieldErrors, toProblem } from '../api/httpClient';
import { useAuthStore } from '../store/authStore';

interface Props {
  mode: 'signIn' | 'signUp';
}

export default function AuthPage({ mode }: Props) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);

  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<string | null>(null);

  // Where the user was heading before being asked to sign in.
  const returnTo = (location.state as { from?: string } | null)?.from ?? '/reserver';

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setErrors({});
    setFailure(null);

    const form = new FormData(event.currentTarget);
    const email = String(form.get('email') ?? '').trim();
    const password = String(form.get('password') ?? '');

    try {
      const session =
        mode === 'signIn'
          ? await authApi.login({ email, password })
          : await authApi.register({
              firstName: String(form.get('firstName') ?? '').trim(),
              lastName: String(form.get('lastName') ?? '').trim(),
              email,
              password,
              preferredLanguage: i18n.resolvedLanguage ?? 'fr',
            });

      setSession(session.accessToken, session.user);
      navigate(returnTo, { replace: true });
    } catch (error) {
      const problem = toProblem(error);
      const fields = fieldErrors(problem);

      if (Object.keys(fields).length > 0) {
        setErrors(fields);
      } else {
        // The API answers a wrong password and an unknown account identically,
        // so there is nothing more specific to show here — by design.
        setFailure(problem.detail ?? t('auth.failed'));
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="section">
      <div className="shell shell--narrow">
        <div className="section-head">
          <h2>{t(mode === 'signIn' ? 'auth.signInTitle' : 'auth.signUpTitle')}</h2>
        </div>

        <form className="form" onSubmit={handleSubmit} noValidate>
          {mode === 'signUp' && (
            <div className="field-grid">
              <label className="field">
                <span className="field__label">{t('auth.firstName')}</span>
                <input name="firstName" autoComplete="given-name" required />
                {errors.firstName && <span className="field__error">{errors.firstName}</span>}
              </label>
              <label className="field">
                <span className="field__label">{t('auth.lastName')}</span>
                <input name="lastName" autoComplete="family-name" required />
                {errors.lastName && <span className="field__error">{errors.lastName}</span>}
              </label>
            </div>
          )}

          <label className="field">
            <span className="field__label">{t('auth.email')}</span>
            <input name="email" type="email" autoComplete="email" required />
            {errors.email && <span className="field__error">{errors.email}</span>}
          </label>

          <label className="field">
            <span className="field__label">{t('auth.password')}</span>
            {mode === 'signUp' && <span className="field__hint">{t('auth.passwordHint')}</span>}
            <input
              name="password"
              type="password"
              autoComplete={mode === 'signIn' ? 'current-password' : 'new-password'}
              required
            />
            {errors.password && <span className="field__error">{errors.password}</span>}
          </label>

          {failure && <p className="notice notice--error">{failure}</p>}

          <button type="submit" className="btn btn--primary" disabled={busy}>
            {busy ? t('auth.signingIn') : t(mode === 'signIn' ? 'auth.signIn' : 'auth.signUp')}
          </button>

          <p className="form__note">
            {mode === 'signIn' ? (
              <>
                {t('auth.noAccount')} <Link to="/inscription">{t('auth.signUp')}</Link>
              </>
            ) : (
              <>
                {t('auth.hasAccount')} <Link to="/connexion">{t('auth.signIn')}</Link>
              </>
            )}
          </p>
        </form>
      </div>
    </section>
  );
}
