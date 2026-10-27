import type { SVGProps } from "react";

/** Low coral island: green crown, sand rim, shallow-water halo. No palm clichés, just the shape. */
export function Island({ className, ...rest }: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 200 60" className={className} aria-hidden="true" {...rest}>
      <ellipse cx="100" cy="46" rx="96" ry="9" fill="var(--color-lagoon-200)" opacity="0.65" />
      <path
        d="M12 46c10-8 28-11 50-11 14-4 30-4 44-2 24-1 48 3 82 13z"
        fill="var(--color-sand-300)"
      />
      <path
        d="M30 41c14-8 30-11 48-9 14-5 30-6 44-3 18 0 36 4 52 12-30-4-60-3-92-2-18 0-34 1-52 2z"
        fill="var(--color-lagoon-700)"
      />
    </svg>
  );
}
