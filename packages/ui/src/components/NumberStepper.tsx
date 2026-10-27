import { useId } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

export type NumberStepperProps = {
  value: number;
  onValueChange: (value: number) => void;
  min?: number;
  max?: number;
  /** Visible group label, e.g. "Passengers". */
  label: string;
  decrementLabel: string;
  incrementLabel: string;
  /** Optional readable value, e.g. "2 passengers". Falls back to the number. */
  valueText?: string;
  className?: string;
};

/** Quantity control (passengers). The value is announced politely as it changes. */
export function NumberStepper({
  value,
  onValueChange,
  min = 1,
  max = 9,
  label,
  decrementLabel,
  incrementLabel,
  valueText,
  className,
}: NumberStepperProps) {
  const id = useId();
  const btn =
    "grid size-9 place-items-center rounded-full bg-surface shadow-[var(--shadow-hairline)] text-ink transition-colors hover:bg-lagoon-50 disabled:pointer-events-none disabled:opacity-40";
  return (
    <div role="group" aria-labelledby={id} className={cn("flex items-center gap-3", className)}>
      <span id={id} className="sr-only">
        {label}
      </span>
      <button
        type="button"
        aria-label={decrementLabel}
        className={btn}
        disabled={value <= min}
        onClick={() => onValueChange(Math.max(min, value - 1))}
      >
        <Icon name="minus" size={16} />
      </button>
      <output aria-live="polite" className="tabular min-w-6 text-center text-body font-medium">
        <span className="sr-only">{valueText}</span>
        <span aria-hidden={valueText ? true : undefined}>{value}</span>
      </output>
      <button
        type="button"
        aria-label={incrementLabel}
        className={btn}
        disabled={value >= max}
        onClick={() => onValueChange(Math.min(max, value + 1))}
      >
        <Icon name="plus" size={16} />
      </button>
    </div>
  );
}
