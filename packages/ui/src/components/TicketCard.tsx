import type { ReactNode } from "react";
import { cn } from "../lib/cn";
import { Badge } from "./Badge";
import { ModeChip } from "./TransportIcon";
import type { TransportMode } from "./modes";

export type TicketCardProps = {
  mode: TransportMode;
  /** Accessible mode name, e.g. "Ferry". */
  modeLabel: string;
  fromLabel: string;
  toLabel: string;
  departTime: string;
  arriveTime: string;
  day: string;
  operatorLabel: string;
  /** Passenger name and seat/reference lines, pre-formatted. */
  details: { id: string; label: string; value: string }[];
  status: { label: string; tone: "success" | "caution" | "danger" | "neutral" };
  /** QR element (an <img> or <svg>). Rendered on a white tile for scanning. */
  qr: ReactNode;
  qrCaption?: string;
  className?: string;
};

/** Wallet-style ticket: gradient header strip, white QR tile, diagonal sheen on hover/focus. */
export function TicketCard({
  mode,
  modeLabel,
  fromLabel,
  toLabel,
  departTime,
  arriveTime,
  day,
  operatorLabel,
  details,
  status,
  qr,
  qrCaption,
  className,
}: TicketCardProps) {
  return (
    <article
      aria-label={`${modeLabel}: ${fromLabel} → ${toLabel}, ${day} ${departTime}`}
      tabIndex={0}
      className={cn(
        "group relative w-80 overflow-hidden rounded-xl bg-surface shadow-[var(--shadow-hairline),var(--shadow-raised)] transition-shadow duration-[var(--duration-base)] focus-visible:shadow-[var(--shadow-hairline),var(--shadow-float)] hover:shadow-[var(--shadow-hairline),var(--shadow-float)]",
        className,
      )}
    >
      <header className="relative flex items-center justify-between gap-3 bg-gradient-to-br from-lagoon-500 to-ocean-900 px-5 py-4 text-ink-inverse">
        <div className="flex items-center gap-3">
          <ModeChip mode={mode} size={36} label={modeLabel} className="bg-white/18 text-white" />
          <div>
            <p className="text-small font-medium">{operatorLabel}</p>
            <p className="tabular text-caption text-white/80">{day}</p>
          </div>
        </div>
        <Badge tone={status.tone} size="sm">
          {status.label}
        </Badge>
      </header>
      <div className="px-5 py-5">
        <div className="tabular flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="font-display text-headline leading-none text-ink">{departTime}</p>
            <p className="mt-1 truncate text-small text-ink-muted">{fromLabel}</p>
          </div>
          <span aria-hidden="true" className="h-px w-8 bg-line-strong" />
          <div className="min-w-0 text-end">
            <p className="font-display text-headline leading-none text-ink">{arriveTime}</p>
            <p className="mt-1 truncate text-small text-ink-muted">{toLabel}</p>
          </div>
        </div>
        <dl className="mt-5 grid grid-cols-2 gap-x-4 gap-y-3">
          {details.map((d) => (
            <div key={d.id} className="min-w-0">
              <dt className="text-caption font-medium tracking-wide text-ink-muted uppercase">
                {d.label}
              </dt>
              <dd className="tabular truncate text-small text-ink">{d.value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div className="relative">
        <span
          aria-hidden="true"
          className="absolute -inset-s-3 -top-3 size-6 rounded-full bg-canvas shadow-[inset_0_0_0_1px_var(--color-line)]"
        />
        <span
          aria-hidden="true"
          className="absolute -inset-e-3 -top-3 size-6 rounded-full bg-canvas shadow-[inset_0_0_0_1px_var(--color-line)]"
        />
        <div className="mx-5 border-t-2 border-dashed border-line-strong" />
      </div>
      <div className="flex flex-col items-center gap-2 px-5 py-5">
        <div className="rounded-lg bg-white p-3 shadow-[var(--shadow-hairline)] [&>*]:block [&>*]:size-36">
          {qr}
        </div>
        {qrCaption ? <p className="tabular text-caption text-ink-muted">{qrCaption}</p> : null}
      </div>
      <span
        aria-hidden="true"
        className="sheen pointer-events-none absolute inset-0 opacity-0 group-hover:animate-[dh-sheen_1.1s_var(--ease-calm)_forwards] group-focus-visible:animate-[dh-sheen_1.1s_var(--ease-calm)_forwards] group-hover:opacity-100 group-focus-visible:opacity-100"
      />
    </article>
  );
}
