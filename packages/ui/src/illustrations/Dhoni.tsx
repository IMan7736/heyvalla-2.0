import type { SVGProps } from "react";

/**
 * Original dhoni silhouette: raised curved bow, low stern, cabin with canopy, single mast and
 * lateen sail. Drawn facing the inline-end side. Colours inherit currentColor; windows use `window`.
 */
export function Dhoni({
  className,
  windowColor = "var(--color-sand)",
  ...rest
}: SVGProps<SVGSVGElement> & { windowColor?: string }) {
  return (
    <svg
      viewBox="0 0 240 110"
      className={className}
      fill="currentColor"
      aria-hidden="true"
      {...rest}
    >
      {/* sail and mast */}
      <path
        d="M150 10v52M150 12l-2 0"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinecap="round"
        fill="none"
      />
      <path d="M153 12c24 10 42 28 52 48h-52z" opacity="0.92" />
      <path d="M150 8l9 4-9 3z" fill="var(--color-coral)" />
      {/* cabin */}
      <path d="M62 62V44a5 5 0 0 1 5-5h58a5 5 0 0 1 5 5v18z" />
      <path d="M56 40h80a3 3 0 0 0 0-6H56a3 3 0 0 0 0 6z" />
      <rect x="72" y="46" width="12" height="9" rx="2" fill={windowColor} />
      <rect x="92" y="46" width="12" height="9" rx="2" fill={windowColor} />
      <rect x="112" y="46" width="12" height="9" rx="2" fill={windowColor} />
      {/* hull */}
      <path d="M6 58c34 7 104 8 158 2 24-3 40-18 52-44 1 30-10 56-46 62-52 8-118 6-152-4-8-3-12-10-12-16z" />
      <path
        d="M12 62c40 6 100 7 154 1"
        stroke={windowColor}
        strokeOpacity="0.55"
        strokeWidth="1.6"
        fill="none"
        strokeLinecap="round"
      />
    </svg>
  );
}
