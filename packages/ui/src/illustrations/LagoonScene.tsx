import type { ReactNode } from "react";
import { cn } from "../lib/cn";
import { Dhoni } from "./Dhoni";
import { Island } from "./Island";

/**
 * Living lagoon backdrop for the landing hero. Layers: sky wash, coral sun, far islands, horizon
 * hairline, water gradient, two caustic layers (SVG turbulence rasterised once, then moved by
 * transform only), drifting wave bands, a bobbing dhoni, and a fade into the page canvas.
 * All ambient motion stops under prefers-reduced-motion (see theme.css).
 */
export function LagoonScene({ className, children }: { className?: string; children?: ReactNode }) {
  return (
    <div className={cn("relative isolate overflow-hidden bg-lagoon-50", className)}>
      {/* sky */}
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-[linear-gradient(to_bottom,var(--color-sand-50)_0%,var(--color-lagoon-50)_34%,var(--color-lagoon-200)_41%,var(--color-lagoon-400)_62%,var(--color-lagoon-600)_100%)]"
      />
      {/* sun */}
      <div aria-hidden="true" className="absolute inset-s-[76%] top-[11%] -z-10">
        <div className="size-28 rounded-full bg-coral/30 blur-2xl" />
        <div className="absolute inset-4 rounded-full bg-coral shadow-[0_0_48px_12px_rgb(242_140_120/0.35)]" />
      </div>
      {/* far islands on the horizon */}
      <Island className="absolute inset-s-[8%] top-[35.5%] -z-10 w-56 opacity-80" />
      <Island className="absolute inset-s-[34%] top-[37.5%] -z-10 w-36 opacity-60" />
      <Island className="absolute inset-s-[78%] top-[36.5%] -z-10 w-64 opacity-85" />
      <div
        aria-hidden="true"
        className="absolute inset-x-0 top-[42.6%] -z-10 h-px bg-ocean-900/25"
      />

      {/* caustics */}
      <svg
        aria-hidden="true"
        className="absolute inset-x-[-6%] top-[43%] bottom-0 -z-10 h-[57%] w-[112%] animate-[dh-caustic_26s_ease-in-out_infinite_alternate] will-change-transform mix-blend-soft-light"
        preserveAspectRatio="none"
      >
        <filter id="dh-caustic-a" x="0" y="0" width="100%" height="100%">
          <feTurbulence type="turbulence" baseFrequency="0.011 0.034" numOctaves="2" seed="7" />
          <feColorMatrix values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  -7 0 0 0 1.55" />
        </filter>
        <rect width="100%" height="100%" filter="url(#dh-caustic-a)" opacity="0.7" />
      </svg>
      <svg
        aria-hidden="true"
        className="absolute inset-x-[-6%] top-[43%] bottom-0 -z-10 h-[57%] w-[112%] animate-[dh-caustic_38s_ease-in-out_infinite_alternate-reverse] will-change-transform mix-blend-soft-light"
        preserveAspectRatio="none"
      >
        <filter id="dh-caustic-b" x="0" y="0" width="100%" height="100%">
          <feTurbulence type="turbulence" baseFrequency="0.02 0.05" numOctaves="2" seed="21" />
          <feColorMatrix values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  -8 0 0 0 1.5" />
        </filter>
        <rect width="100%" height="100%" filter="url(#dh-caustic-b)" opacity="0.35" />
      </svg>

      {/* wave bands */}
      <svg
        aria-hidden="true"
        viewBox="0 0 1600 120"
        preserveAspectRatio="none"
        className="absolute inset-x-[-5%] top-[50%] -z-10 h-24 w-[110%] [mask-image:linear-gradient(to_bottom,#000,transparent)] animate-[dh-drift_19s_ease-in-out_infinite_alternate] will-change-transform"
      >
        <path
          d="M0 60c100-26 200-26 300 0s200 26 300 0 200-26 300 0 200 26 300 0 200-26 400 0v60H0z"
          fill="#fff"
          opacity="0.1"
        />
      </svg>
      <svg
        aria-hidden="true"
        viewBox="0 0 1600 120"
        preserveAspectRatio="none"
        className="absolute inset-x-[-5%] top-[66%] -z-10 h-28 w-[110%] [mask-image:linear-gradient(to_bottom,#000,transparent)] animate-[dh-drift_27s_ease-in-out_infinite_alternate-reverse] will-change-transform"
      >
        <path
          d="M0 70c120-30 240-30 360 0s240 30 360 0 240-30 360 0 240 30 520 0v50H0z"
          fill="#fff"
          opacity="0.08"
        />
      </svg>

      {/* dhoni */}
      <div className="absolute inset-s-[1.5%] top-[45.5%] -z-10 w-40 animate-[dh-bob_7s_ease-in-out_infinite]">
        <Dhoni className="w-full text-ocean-950 drop-shadow-[0_6px_10px_rgb(11_35_40/0.35)]" />
        <div
          aria-hidden="true"
          className="mx-auto -mt-1 h-3 w-[92%] rounded-[50%] bg-white/25 blur-[3px]"
        />
      </div>

      {/* fade into the page */}
      <div
        aria-hidden="true"
        className="absolute inset-x-0 bottom-0 -z-10 h-40 bg-gradient-to-b from-transparent to-canvas"
      />
      {children}
    </div>
  );
}
