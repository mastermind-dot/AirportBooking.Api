import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import Icon from '../ui/Icon';

const LANGUAGES = ['fr', 'en'] as const;

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  const active = i18n.resolvedLanguage ?? 'en';

  // A menu that only closes on its own button is a menu that stays open behind
  // whatever the visitor clicks next.
  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  function choose(code: string) {
    void i18n.changeLanguage(code);
    // Keeps the document language in step, which screen readers and browser
    // translation prompts both read.
    document.documentElement.lang = code;
    setOpen(false);
  }

  return (
    <div className="lang" ref={root}>
      <button
        type="button"
        className="lang__toggle"
        aria-expanded={open}
        aria-haspopup="true"
        aria-label={t('language.label')}
        onClick={() => setOpen((value) => !value)}
      >
        {active.toUpperCase()}
        <Icon name="chevron-down" size={14} strokeWidth={2} />
      </button>

      {open && (
        <div className="lang__menu" role="menu">
          {LANGUAGES.map((code) => (
            <button
              key={code}
              type="button"
              role="menuitem"
              aria-current={active === code}
              onClick={() => choose(code)}
            >
              {t(`language.${code}`)}
              {active === code && <Icon name="check" size={15} strokeWidth={2.2} />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
