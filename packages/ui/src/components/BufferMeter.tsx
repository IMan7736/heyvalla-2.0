import { cn } from "../lib/cn";

export type BufferMeterProps = {
  minutes: number;
  /** Hard minimum buffer for this connection. */
  requiredMinutes: number;
  /** Buffer at which the connection is comfortable. */
  comfortMinutes: number;
  /** Accessible name, e.g. "Connection buffer". */
  label: string;
  /** Plain-language value, e.g. "35 min, comfortable". Shown and used as aria-valuetext. */
  valueText: string;
  /** Short markers under the track. */
  requiredLabel?: string;
  comfortLabel?: string;
  className?: string;
};

/**
 * Horizontal meter of a connection buffer against its required minimum and comfort threshold.
 * Zones: below required (hatched danger), required to comfort (caution), above comfort (success).
 */
export function BufferMeter({
  minutes,
  requiredMinutes,
  comfortMinutes,
  label,
  valueText,
  requiredLabel,
  comfortLabel,
  className,
}: BufferMeterProps) {
  const max = Math.max(comfortMinutes * 1.6, minutes * 1.1, 1);
  const pct = (m: number) => `${Math.min(100, Math.max(0, (m / max) * 100))}%`;
  const state =
    minutes < requiredMinutes ? "danger" : minutes < comfortMinutes ? "caution" : "success";
  const tone = { danger: "text-danger", caution: "text-caution", success: "text-success" }[state];
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-caption font-medium text-ink-muted">{label}</span>
        <span className={cn("tabular text-small font-medium", tone)}>{valueText}</span>
      </div>
      <div
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={Math.round(max)}
        aria-valuenow={Math.round(minutes)}
        aria-valuetext={valueText}
        className="relative h-3 overflow-hidden rounded-full bg-sand-200"
      >
        <span
          aria-hidden="true"
          className="absolute inset-y-0 inset-s-0 bg-[repeating-linear-gradient(135deg,var(--color-danger-soft)_0_4px,#f3c4bf_4px_8px)]"
          style={{ width: pct(requiredMinutes) }}
        />
        <span
          aria-hidden="true"
          className="absolute inset-y-0 bg-caution-soft"
          style={{
            insetInlineStart: pct(requiredMinutes),
            width: `calc(${pct(comfortMinutes)} - ${pct(requiredMinutes)})`,
          }}
        />
        <span
          aria-hidden="true"
          className="absolute inset-y-0 inset-e-0 bg-success-soft"
          style={{ insetInlineStart: pct(comfortMinutes) }}
        />
        <span
          aria-hidden="true"
          className={cn(
            "absolute inset-y-0 inset-s-0 rounded-full",
            state === "danger" ? "bg-danger" : state === "caution" ? "bg-caution" : "bg-success",
          )}
          style={{ width: pct(minutes) }}
        />
      </div>
      {requiredLabel || comfortLabel ? (
        <div aria-hidden="true" className="relative h-4 text-caption text-ink-muted">
          {requiredLabel ? (
            <span
              className="absolute -translate-x-1/2 rtl:translate-x-1/2"
              style={{ insetInlineStart: pct(requiredMinutes) }}
            >
              {requiredLabel}
            </span>
          ) : null}
          {comfortLabel ? (
            <span
              className="absolute -translate-x-1/2 rtl:translate-x-1/2"
              style={{ insetInlineStart: pct(comfortMinutes) }}
            >
              {comfortLabel}
            </span>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
