import type { SVGProps } from "react";
import { Dhoni } from "./Dhoni";

/** Small spot illustrations for empty / error / offline states. 160x120, tonal, original. */
export function EmptyLagoonArt({ className, ...rest }: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 160 120" className={className} aria-hidden="true" {...rest}>
      <circle cx="116" cy="34" r="12" fill="var(--color-coral)" />
      <rect x="0" y="62" width="160" height="58" rx="10" fill="var(--color-lagoon-100)" />
      <path d="M0 62h160" stroke="var(--color-ocean-900)" strokeOpacity="0.3" />
      <path
        d="M26 58c10-7 24-9 38-8 10-3 22-3 32 0 14 1 28 4 38 8z"
        fill="var(--color-lagoon-700)"
      />
      <path d="M20 62c14-4 30-5 46-4 12-2 24-2 36 0 16-1 30 1 42 4z" fill="var(--color-sand-300)" />
      <path
        d="M14 84c10-5 14 5 24 0s14 5 24 0 14 5 24 0 14 5 24 0 14 5 24 0"
        fill="none"
        stroke="#fff"
        strokeOpacity="0.7"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
      <path
        d="M30 100c10-5 14 5 24 0s14 5 24 0 14 5 24 0"
        fill="none"
        stroke="#fff"
        strokeOpacity="0.5"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function ReefSnagArt({ className, ...rest }: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 160 120" className={className} aria-hidden="true" {...rest}>
      <rect x="0" y="48" width="160" height="72" rx="10" fill="var(--color-lagoon-100)" />
      <path d="M0 48h160" stroke="var(--color-ocean-900)" strokeOpacity="0.3" />
      <path d="M44 120c-2-22 6-40 18-52 4 14 2 30 10 52z" fill="var(--color-coral-300)" />
      <path d="M78 120c0-18 8-30 20-40 6 12 4 26 14 40z" fill="var(--color-coral)" />
      <path d="M104 120c2-16 10-26 22-34 2 12 0 22 8 34z" fill="var(--color-coral-300)" />
      <g transform="translate(30 6) rotate(-14 40 28)" color="var(--color-ocean-900)">
        <Dhoni x="0" y="0" width="84" height="38" />
      </g>
      <path
        d="M28 98c10-5 14 5 24 0"
        fill="none"
        stroke="#fff"
        strokeOpacity="0.7"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function DriftingDhoniArt({ className, ...rest }: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 160 120" className={className} aria-hidden="true" {...rest}>
      <rect x="0" y="58" width="160" height="62" rx="10" fill="var(--color-lagoon-100)" />
      <path d="M0 58h160" stroke="var(--color-ocean-900)" strokeOpacity="0.3" />
      <g color="var(--color-ocean-900)">
        <Dhoni x="32" y="26" width="96" height="44" />
      </g>
      <path
        d="M20 88c10-5 14 5 24 0s14 5 24 0 14 5 24 0 14 5 24 0 14 5 24 0"
        fill="none"
        stroke="#fff"
        strokeOpacity="0.7"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
      <path
        d="M34 104c10-5 14 5 24 0s14 5 24 0"
        fill="none"
        stroke="#fff"
        strokeOpacity="0.5"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
      <circle
        cx="132"
        cy="24"
        r="9"
        fill="none"
        stroke="var(--color-coral)"
        strokeWidth="2.5"
        strokeDasharray="3 4"
      />
    </svg>
  );
}
