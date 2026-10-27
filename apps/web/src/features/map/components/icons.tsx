import type { TransportMode } from "../types";

interface IconProps {
  size?: number;
  className?: string;
}

const base = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.8,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  "aria-hidden": true,
  focusable: false,
};

export function PlaneIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path
        d="M12 3c.8 0 1.3.6 1.3 1.4v5.3l7 4.5v2l-7-2.2v4l2.1 1.6v1.6L12 20l-3.4 1.2v-1.6l2.1-1.6v-4l-7 2.2v-2l7-4.5V4.4C10.7 3.6 11.2 3 12 3z"
        fill="currentColor"
        stroke="none"
      />
    </svg>
  );
}

export function BoatIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M12 3v9M8.5 7h7M4 14l2 5h12l2-5-8 2.5L4 14zM3 21c1.5 0 1.5-.8 3-.8s1.5.8 3 .8 1.5-.8 3-.8 1.5.8 3 .8 1.5-.8 3-.8 1.5.8 3 .8" />
    </svg>
  );
}

export function SpeedboatIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M3 15l2.5 3.5h13L21 13l-8 1.2-3-4.2H6.5L8 13.5 3 15zM3 21c1.5 0 1.5-.8 3-.8s1.5.8 3 .8 1.5-.8 3-.8 1.5.8 3 .8 1.5-.8 3-.8 1.5.8 3 .8" />
    </svg>
  );
}

export function CarIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M5 16V12l1.6-4.2A2 2 0 0 1 8.5 6.5h7a2 2 0 0 1 1.9 1.3L19 12v4M4 16h16v2.5H4zM7 18.5V20M17 18.5V20M7.5 13h.01M16.5 13h.01" />
    </svg>
  );
}

export function BusIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M6 4.5h12a1.5 1.5 0 0 1 1.5 1.5v10.5h-15V6A1.5 1.5 0 0 1 6 4.5zM4.5 11h15M7 16.5V19M17 16.5V19M8 14h.01M16 14h.01" />
    </svg>
  );
}

export function WalkIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M13 4.5a1.2 1.2 0 1 1-2.4 0 1.2 1.2 0 0 1 2.4 0zM9.5 20l1.7-6.5-2-2 1-4.5 3.3 2.2 2 3M12.2 13.5l2.3 2.5.8 4" />
    </svg>
  );
}

export function PinIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M12 21s6.5-5.6 6.5-11a6.5 6.5 0 1 0-13 0C5.5 15.4 12 21 12 21zM12 12.2a2.2 2.2 0 1 0 0-4.4 2.2 2.2 0 0 0 0 4.4z" />
    </svg>
  );
}

export function PlayIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M8 5.5v13l11-6.5z" fill="currentColor" />
    </svg>
  );
}

export function PauseIcon({ size = 18, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M8 5.5h2.6v13H8zM13.4 5.5H16v13h-2.6z" fill="currentColor" stroke="none" />
    </svg>
  );
}

export function FitIcon({ size = 16, className }: IconProps) {
  return (
    <svg {...base} width={size} height={size} className={className}>
      <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />
    </svg>
  );
}

export function ModeIcon({ mode, ...props }: IconProps & { mode: TransportMode }) {
  switch (mode) {
    case "domestic_flight":
    case "seaplane":
      return <PlaneIcon {...props} />;
    case "ferry":
      return <BoatIcon {...props} />;
    case "speedboat":
    case "resort_transfer":
      return <SpeedboatIcon {...props} />;
    case "bus":
      return <BusIcon {...props} />;
    case "walk":
      return <WalkIcon {...props} />;
    default:
      return <CarIcon {...props} />;
  }
}
