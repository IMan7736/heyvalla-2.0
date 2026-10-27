import { cn } from "../lib/cn";

export type SpinnerProps = {
  size?: number;
  /** Accessible name when the spinner stands alone. */
  label?: string;
  /** Hide from assistive tech (use when the parent already announces busy state). */
  decorative?: boolean;
  className?: string;
};

export function Spinner({ size = 20, label, decorative, className }: SpinnerProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      className={cn("animate-[dh-spin_0.9s_linear_infinite]", className)}
      role={decorative ? undefined : "status"}
      aria-label={decorative ? undefined : label}
      aria-hidden={decorative || undefined}
    >
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity="0.2" strokeWidth="2.5" />
      <path
        d="M21 12a9 9 0 0 0-9-9"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
    </svg>
  );
}
