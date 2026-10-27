import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

export type RiskLevel = "low" | "medium" | "high";

export type RiskGaugeProps = {
  /** 0..1 probability of missing a connection. */
  missProbability: number;
  level: RiskLevel;
  /** Accessible name, e.g. "Risk of missing a connection". */
  label: string;
  /** Level in words, e.g. "Low risk". Always visible. */
  levelLabel: string;
  /** Percentage text, e.g. "2%". */
  valueText: string;
  variant?: "gauge" | "bar";
  className?: string;
};

const tones = {
  low: { stroke: "var(--color-success)", text: "text-success", icon: "shield" },
  medium: { stroke: "var(--color-caution)", text: "text-caution", icon: "alert" },
  high: { stroke: "var(--color-danger)", text: "text-danger", icon: "alert" },
} as const;

/** Semicircle gauge (or compact bar) for miss-risk. Level text and icon always accompany colour. */
export function RiskGauge({
  missProbability,
  level,
  label,
  levelLabel,
  valueText,
  variant = "gauge",
  className,
}: RiskGaugeProps) {
  const p = Math.min(1, Math.max(0, missProbability));
  // Visual scale: 0..30% fills the dial, anything above is full. Keeps small risks readable.
  const fill = Math.min(1, p / 0.3);
  const tone = tones[level];
  const aria = {
    role: "meter",
    "aria-label": label,
    "aria-valuemin": 0,
    "aria-valuemax": 100,
    "aria-valuenow": Math.round(p * 100),
    "aria-valuetext": `${levelLabel}, ${valueText}`,
  } as const;

  if (variant === "bar") {
    return (
      <div className={cn("flex flex-col gap-1", className)}>
        <div className={cn("flex items-center gap-1.5 text-small font-medium", tone.text)}>
          <Icon name={tone.icon} size={16} />
          <span>{levelLabel}</span>
          <span className="tabular text-ink-muted">{valueText}</span>
        </div>
        <div {...aria} className="h-1.5 overflow-hidden rounded-full bg-sand-200">
          <span
            aria-hidden="true"
            className="block h-full rounded-full"
            style={{ width: `${Math.max(6, fill * 100)}%`, backgroundColor: tone.stroke }}
          />
        </div>
      </div>
    );
  }

  const r = 42;
  const arc = Math.PI * r;
  return (
    <div className={cn("inline-flex flex-col items-center", className)}>
      <svg viewBox="0 0 100 56" width={120} height={67} {...aria}>
        <path
          d="M8 50a42 42 0 0 1 84 0"
          fill="none"
          stroke="var(--color-sand-200)"
          strokeWidth="9"
          strokeLinecap="round"
        />
        <path
          d="M8 50a42 42 0 0 1 84 0"
          fill="none"
          stroke={tone.stroke}
          strokeWidth="9"
          strokeLinecap="round"
          strokeDasharray={`${Math.max(0.04, fill) * arc} ${arc}`}
        />
        <text
          x="50"
          y="46"
          textAnchor="middle"
          className="tabular"
          fontSize="17"
          fontWeight="600"
          fill="var(--color-ocean-900)"
        >
          {valueText}
        </text>
      </svg>
      <p className={cn("-mt-1 flex items-center gap-1 text-small font-medium", tone.text)}>
        <Icon name={tone.icon} size={14} />
        {levelLabel}
      </p>
    </div>
  );
}
