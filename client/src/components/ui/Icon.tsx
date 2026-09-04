/**
 * The icon set, as one component.
 *
 * Every glyph is a 24-grid stroke path inheriting `currentColor` and the local
 * font size, so an icon beside a label always matches it. Inline rather than an
 * icon font or a sprite request: there are a dozen of them, they are needed on
 * first paint inside the header and the search card, and a font would flash.
 */

export type IconName =
  | 'arrow-right'
  | 'arrow-left'
  | 'calendar'
  | 'check'
  | 'chevron-down'
  | 'clock'
  | 'close'
  | 'globe'
  | 'mail'
  | 'menu'
  | 'phone'
  | 'pin'
  | 'plane'
  | 'search'
  | 'shield'
  | 'star'
  | 'swap'
  | 'users';

const PATHS: Record<IconName, string> = {
  'arrow-right': 'M5 12h14M13 6l6 6-6 6',
  'arrow-left': 'M19 12H5M11 18l-6-6 6-6',
  calendar: 'M8 3v4M16 3v4M3.5 9.5h17M5 5h14a1.5 1.5 0 0 1 1.5 1.5v13A1.5 1.5 0 0 1 19 21H5a1.5 1.5 0 0 1-1.5-1.5v-13A1.5 1.5 0 0 1 5 5Z',
  check: 'M4 12.5l5.5 5.5L20 7',
  'chevron-down': 'M6 9.5l6 6 6-6',
  clock: 'M12 7v5.2l3.2 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  close: 'M6 6l12 12M18 6L6 18',
  globe: 'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0ZM3.5 9h17M3.5 15h17M12 3a15 15 0 0 1 0 18 15 15 0 0 1 0-18Z',
  mail: 'M3.5 7.5 12 13l8.5-5.5M4.5 5.5h15a1.5 1.5 0 0 1 1.5 1.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17V7a1.5 1.5 0 0 1 1.5-1.5Z',
  menu: 'M4 7h16M4 12h16M4 17h16',
  phone: 'M7.5 3.5h-2A2.5 2.5 0 0 0 3 6.2C3 14 10 21 17.8 21a2.5 2.5 0 0 0 2.7-2.5v-2l-4.2-1.6-1.9 2.1a13.8 13.8 0 0 1-5.4-5.4l2.1-1.9L9.5 5.6Z',
  pin: 'M12 21s7-5.6 7-11a7 7 0 1 0-14 0c0 5.4 7 11 7 11ZM12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z',
  plane: 'M10.2 20.5 12 15l6.2-2.1 3-1a1.7 1.7 0 0 0-.6-3.3l-4.4.4-4.6-5.3a1.2 1.2 0 0 0-2.1.9l.6 5.6-3.9 1.4-2.1-1.9a.9.9 0 0 0-1.4 1l1.6 3.4-.7 2.6 2.5-1 3.4 1.7a.9.9 0 0 0 1-1.4Z',
  search: 'M20 20l-3.4-3.4M18.5 11a7.5 7.5 0 1 1-15 0 7.5 7.5 0 0 1 15 0Z',
  shield: 'M12 21c4.5-2 7-5.4 7-9.4V6.3L12 3.5 5 6.3v5.3c0 4 2.5 7.4 7 9.4ZM9 12l2.2 2.2L15.4 10',
  star: 'm12 3.8 2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9L12 3.8Z',
  swap: 'M7 4 3.5 7.5 7 11M3.5 7.5H20M17 13l3.5 3.5L17 20M20.5 16.5H4',
  users: 'M16 20v-1.5a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4V20M13 7a3.5 3.5 0 1 1-7 0 3.5 3.5 0 0 1 7 0ZM17 7.2a3 3 0 0 1 0 5.6M21 20v-1.4a3.6 3.6 0 0 0-2.4-3.3',
};

interface IconProps {
  name: IconName;
  /** Pixels. Defaults to 20, which lines up with body text. */
  size?: number;
  strokeWidth?: number;
  className?: string;
}

export default function Icon({ name, size = 20, strokeWidth = 1.7, className }: IconProps) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
