import { Fragment } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";
import { ModeChip } from "./TransportIcon";
import type { TransportMode } from "./modes";

export type RibbonLeg = {
  id: string;
  mode: TransportMode;
  durationMin: number;
  /** Pre-formatted duration, e.g. "45 min". */
  durationLabel: string;
  /** Accessible description, e.g. "Ferry from Maafushi to Malé, 45 min". */
  description: string;
  /** Place name at the start of the leg (shown in the detailed variant). */
  fromLabel?: string;
};

export type RibbonBuffer = {
  afterLegIndex: number;
  level: "comfortable" | "ok" | "tight";
  /** Pre-formatted, e.g. "35 min buffer". */
  label: string;
  /** Short text for the level, shown in the detailed variant and read by screen readers. */
  levelLabel: string;
};

export type JourneyRibbonProps = {
  legs: RibbonLeg[];
  buffers?: RibbonBuffer[];
  /** Final destination name (detailed variant). */
  destinationLabel?: string;
  /** Accessible list name, e.g. "Journey: 4 legs". */
  label: string;
  variant?: "compact" | "detailed";
  className?: string;
};

const bufferTone = {
  comfortable: { dot: "bg-success", text: "text-success", icon: "check-circle" },
  ok: { dot: "bg-lagoon-500", text: "text-lagoon-700", icon: "clock" },
  tight: { dot: "bg-caution", text: "text-caution", icon: "alert" },
} as const;

/**
 * One continuous line through the whole trip: mode chips sized by duration, nodes at every
 * interchange, buffers drawn as labelled gaps (icon + text + colour, never colour alone).
 */
export function JourneyRibbon({
  legs,
  buffers = [],
  destinationLabel,
  label,
  variant = "compact",
  className,
}: JourneyRibbonProps) {
  const detailed = variant === "detailed";
  return (
    <ol aria-label={label} className={cn("flex w-full items-start", detailed && "pb-7", className)}>
      {legs.map((leg, i) => {
        const buffer = buffers.find((b) => b.afterLegIndex === i);
        const grow = Math.max(14, leg.durationMin) ** 0.65;
        return (
          <Fragment key={leg.id}>
            <li
              className="flex min-w-0 flex-col gap-1.5"
              style={{ flexGrow: grow, flexBasis: 0, minInlineSize: detailed ? 92 : 72 }}
            >
              <div className="flex items-center">
                <span aria-hidden="true" className="size-2.5 shrink-0 rounded-full bg-ocean-900" />
                <span aria-hidden="true" className="h-0.5 flex-1 bg-ocean-900/80" />
                <ModeChip mode={leg.mode} size={detailed ? 40 : 30} />
                <span aria-hidden="true" className="h-0.5 flex-1 bg-ocean-900/80" />
              </div>
              <div className="min-w-0 ps-0.5">
                {detailed && leg.fromLabel ? (
                  <p className="truncate text-small font-medium text-ink">{leg.fromLabel}</p>
                ) : null}
                <p className="tabular whitespace-nowrap text-caption text-ink-muted">
                  {leg.durationLabel}
                </p>
                <span className="sr-only">{leg.description}</span>
              </div>
            </li>
            {buffer ? (
              <li
                className="flex shrink-0 flex-col items-center gap-1.5 px-1"
                style={{ minInlineSize: detailed ? 96 : 56 }}
              >
                <div className="flex w-full items-center">
                  <span
                    aria-hidden="true"
                    className="h-0 flex-1 border-t-2 border-dotted border-ocean-500"
                  />
                  <span
                    aria-hidden="true"
                    className={cn("mx-1 size-2.5 rounded-full", bufferTone[buffer.level].dot)}
                  />
                  <span
                    aria-hidden="true"
                    className="h-0 flex-1 border-t-2 border-dotted border-ocean-500"
                  />
                </div>
                <p
                  className={cn(
                    "tabular flex items-center gap-1 text-caption font-medium",
                    bufferTone[buffer.level].text,
                  )}
                >
                  <Icon name={bufferTone[buffer.level].icon} size={12} />
                  {buffer.label}
                  <span className="sr-only">: {buffer.levelLabel}</span>
                </p>
              </li>
            ) : null}
          </Fragment>
        );
      })}
      <li
        className="relative flex w-4 shrink-0 items-center self-start"
        style={{ blockSize: detailed ? 40 : 30 }}
      >
        <span className="size-4 rounded-full bg-coral shadow-[0_0_0_4px_var(--color-coral-100)]" />
        {detailed && destinationLabel ? (
          <p className="absolute inset-e-0 top-full mt-2 whitespace-nowrap text-end text-small font-medium text-ink">
            {destinationLabel}
          </p>
        ) : (
          <span className="sr-only">{destinationLabel}</span>
        )}
      </li>
    </ol>
  );
}
