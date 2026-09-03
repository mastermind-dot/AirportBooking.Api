import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import fr from './fr.json';
import en from './en.json';

/**
 * English is the configured fallback, but detection runs first — and since
 * Malu Aviation operates in the DRC, most visitors arrive with a French locale
 * and land on French without touching the switcher. The choice is remembered in
 * localStorage: a language preference is not a secret.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      fr: { translation: fr },
      en: { translation: en },
    },
    fallbackLng: 'en',
    supportedLngs: ['fr', 'en'],

    // "fr-CD" and "fr-BE" should both resolve to "fr" rather than falling
    // through to English.
    load: 'languageOnly',

    detection: {
      order: ['localStorage', 'navigator', 'htmlTag'],
      caches: ['localStorage'],
      lookupLocalStorage: 'malu-lang',
    },

    interpolation: {
      // React escapes for us; doing it twice mangles apostrophes in French copy.
      escapeValue: false,
    },
  });

export default i18n;
