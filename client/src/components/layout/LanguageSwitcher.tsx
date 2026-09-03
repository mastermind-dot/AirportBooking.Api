import { useTranslation } from 'react-i18next';

const LANGUAGES = ['fr', 'en'] as const;

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const active = i18n.resolvedLanguage ?? 'en';

  return (
    <div className="lang" role="group" aria-label={t('language.label')}>
      {LANGUAGES.map((code) => (
        <button
          key={code}
          type="button"
          aria-pressed={active === code}
          onClick={() => {
            void i18n.changeLanguage(code);
            // Keeps the document language in step, which screen readers and
            // browser translation prompts both read.
            document.documentElement.lang = code;
          }}
        >
          {code.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
