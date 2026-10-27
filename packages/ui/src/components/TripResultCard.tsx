import type { ReactNode } from "react";
import { cn } from "../lib/cn";
import { Pill } from "./Badge";
import { JourneyRibbon, type JourneyRibbonProps } from "./JourneyRibbon";
import { RiskGauge, type RiskGaugeProps } from "./RiskGauge";

export type TripLabelTone = "lagoon" | "coral" | "success" | "neutral";
export type TripResultCardProps = {
  /** Display strings are pre-formatted by the caller (Intl, Maldives time, next-intl). */
  departTime: string;
  arriveTime: string;
  /** e.g. "Tue 27 Oct". */
  arriveDay: string;
  durationLabel: string;
  transfersLabel: string;
  price: string;
  /** e.g. "for 2 passengers". */
  priceNote?: string;
  labels?: { id: string; label: string; tone?: TripLabelTone }[];
  ribbon: JourneyRibbonProps;
  risk: RiskGaugeProps;
  /** "Why this trip" bullets in plain language, already translated. */
  explanations?: { id: string; text: string; severity: "info" | "good" | "caution" }[];
  explanationsTitle?: string;
  selected?: boolean;
  /** Accessible name of the card region, e.g. "Trip arriving 16:40, MVR 450". */
  label: string;
  /** Primary action in the stub (select / view details). */
  action?: ReactNode;
  /** Adds a hover lift. The card itself is not a button: put the interactive control in `action`. */
  interactive?: boolean;
  className?: string;
};

const labelTone: Record<TripLabelTone, "lagoon" | "coral" | "success" | "neutral"> = {
  lagoon: "lagoon",
  coral: "coral",
  success: "success",
  neutral: "neutral",
};

/**
 * Result card styled as a boarding pass. Hierarchy: arrival time (display serif) > price >
 * risk > journey ribbon > explanation. Left main section, perforated right stub.
 */
export function TripResultCard({
  departTime,
  arriveTime,
  arriveDay,
  durationLabel,
  transfersLabel,
  price,
  priceNote,
  labels = [],
  ribbon,
  risk,
  explanations = [],
  explanationsTitle,
  selected,
  label,
  action,
  interactive,
  className,
}: TripResultCardProps) {
  return (
    <article
      aria-label={label}
      data-selected={selected || undefined}
      data-interactive={interactive ? "" : undefined}
      className={cn("pass", className)}
    >
      <div
        className="pass-body relative grid bg-surface"
        style={{ gridTemplateColumns: "minmax(0,1fr) var(--stub)" }}
      >
        <div className="flex min-w-0 flex-col gap-5 p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-caption font-medium tracking-wide text-ink-muted uppercase">
                {arriveDay}
              </p>
              <p className="tabular flex items-baseline gap-3">
                <span className="font-display text-display text-ink">{arriveTime}</span>
                <span className="text-small text-ink-muted">
                  <span className="sr-only">{departTime} → </span>
                  <span aria-hidden="true">{departTime} →</span>
                </span>
              </p>
              <p className="tabular text-small text-ink-muted">
                {durationLabel} · {transfersLabel}
              </p>
            </div>
            {labels.length > 0 ? (
              <ul className="flex flex-wrap justify-end gap-1.5">
                {labels.map((l) => (
                  <li key={l.id}>
                    <Pill dot={l.tone === "coral"} tone={labelTone[l.tone ?? "lagoon"]}>
                      {l.label}
                    </Pill>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
          <JourneyRibbon {...ribbon} />
          {explanations.length > 0 ? (
            <div>
              {explanationsTitle ? (
                <p className="mb-1.5 text-caption font-medium text-ink-muted">
                  {explanationsTitle}
                </p>
              ) : null}
              <ul className="flex flex-wrap gap-x-5 gap-y-1 text-small text-ink">
                {explanations.map((e) => (
                  <li key={e.id} className="flex items-center gap-2">
                    <span
                      aria-hidden="true"
                      className={cn(
                        "size-1.5 rounded-full",
                        e.severity === "good" && "bg-success",
                        e.severity === "caution" && "bg-caution",
                        e.severity === "info" && "bg-lagoon-500",
                      )}
                    />
                    {e.text}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
        <div
          className="pass-perforation absolute inset-y-4 w-px"
          style={{ insetInlineStart: "calc(100% - var(--stub))" }}
          aria-hidden="true"
        />
        <div className="flex flex-col items-center justify-between gap-4 bg-lagoon-50/60 p-6 text-center">
          <div>
            <p className="tabular font-display text-headline text-ink">{price}</p>
            {priceNote ? <p className="text-caption text-ink-muted">{priceNote}</p> : null}
          </div>
          <RiskGauge {...risk} />
          {action ?? null}
        </div>
      </div>
    </article>
  );
}
