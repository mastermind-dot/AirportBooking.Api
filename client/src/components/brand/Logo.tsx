/**
 * The brand lockup: a swept wing mark beside the wordmark.
 *
 * Drawn rather than loaded as a bitmap, because the header sits on navy and the
 * footer on navy while inner pages want it on white — one SVG that inherits
 * `currentColor` covers all three, stays sharp on any display, and costs no
 * request. The company's raster wordmark is still in /images/logo.png for
 * documents and email signatures.
 */

interface LogoProps {
  /** `ink` renders the navy version, for light backgrounds. */
  variant?: 'light' | 'ink';
  className?: string;
}

export default function Logo({ variant = 'light', className }: LogoProps) {
  return (
    <span className={['logo', variant === 'ink' ? 'logo--ink' : '', className].filter(Boolean).join(' ')}>
      <svg
        className="logo__mark"
        width="42"
        height="22"
        viewBox="0 0 52 27"
        fill="currentColor"
        aria-hidden="true"
        focusable="false"
      >
        {/* Leading sweep: the long upper wing. */}
        <path d="M52 0C35.6 1.5 21.9 6.6 10.8 14.8 6.6 17.9 3 21.5 0 25.6h10.4c2.9-3.3 6.3-6.3 10.1-9C28.9 10.7 39.3 5.2 52 0Z" />
        {/* Trailing feather, shorter and tucked underneath. */}
        <path d="M20.6 25.6h10.2c4-4.1 9-7.6 14.9-10.5-9.6 2.1-18 5.6-25.1 10.5Z" opacity=".72" />
      </svg>

      <span className="logo__word">
        <b>Malu</b>
        <span>Aviation</span>
      </span>
    </span>
  );
}
