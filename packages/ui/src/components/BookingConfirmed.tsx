import type { ReactNode } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

/**
 * Confirmation moment: ripple rings expand from a check mark, a horizon line draws across.
 * Settles in about a second; static when the user prefers reduced motion.
 */
export function BookingConfirmed({
  title,
  description,
  reference,
  referenceLabel,
  children,
  className,
}: {
  title: string;
  description?: string;
  /** Booking reference, shown in tabular figures. */
  reference?: string;
  referenceLabel?: string;
  children?: ReactNode;
  className?: string;
}) {
  return (
    <section
      aria-labelledby="booking-confirmed-title"
      className={cn(
        "relative flex flex-col items-center gap-4 overflow-hidden px-8 py-12 text-center",
        className,
      )}
    >
      <div className="relative grid size-28 place-items-center">
        {[0, 1, 2].map((i) => (
          <span
            key={i}
            aria-hidden="true"
            className="absolute inset-0 rounded-full border border-lagoon-400 opacity-0 motion-safe:animate-[dh-ripple_1.8s_var(--ease-calm)_forwards]"
            style={{ animationDelay: `${i * 220}ms` }}
          />
        ))}
        <span className="relative grid size-16 place-items-center rounded-full bg-action text-ink-inverse shadow-[var(--shadow-float)] motion-safe:animate-[dh-pop-in_var(--duration-slow)_var(--ease-swell)_both]">
          <Icon name="check" size={32} strokeWidth={2.4} />
        </span>
      </div>
      <h2 id="booking-confirmed-title" className="font-display text-headline text-ink">
        {title}
      </h2>
      {description ? <p className="max-w-prose text-body text-ink-muted">{description}</p> : null}
      {reference ? (
        <p className="tabular rounded-full bg-sand-200 px-4 py-1.5 text-small text-ink">
          {referenceLabel ? <span className="text-ink-muted">{referenceLabel} </span> : null}
          <span className="font-medium tracking-wider">{reference}</span>
        </p>
      ) : null}
      <svg aria-hidden="true" viewBox="0 0 320 8" className="mt-2 h-2 w-80 max-w-full">
        <path
          d="M0 4h320"
          stroke="var(--color-ocean-900)"
          strokeWidth="1.5"
          strokeDasharray="320"
          strokeDashoffset="320"
          className="motion-safe:animate-[dh-draw_1.2s_var(--ease-calm)_0.3s_forwards] motion-reduce:[stroke-dashoffset:0]"
        />
      </svg>
      {children}
    </section>
  );
}
