import type { ReactNode } from 'react';

/**
 * The navy band every inner page opens with.
 *
 * The home page earns a photograph; the rest earn a consistent header, which is
 * what stops the site reading as a set of unrelated forms once a visitor leaves
 * the front page.
 */
interface PageHeroProps {
  eyebrow?: string;
  title: string;
  lead?: ReactNode;
  children?: ReactNode;
}

export default function PageHero({ eyebrow, title, lead, children }: PageHeroProps) {
  return (
    <section className="page-hero">
      <div className="shell">
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {lead && <p className="lead">{lead}</p>}
        {children}
      </div>
    </section>
  );
}
