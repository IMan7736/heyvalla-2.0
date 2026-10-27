"use client";

import { usePrefersReducedMotion } from "@dhathuru/motion";
import dynamic from "next/dynamic";
import { useEffect, useRef, useState, useSyncExternalStore, type ReactNode } from "react";
import { readDeviceSignals, shouldAnimateAtmosphere } from "./atmosphere-policy";

const AtmosphereCanvas = dynamic(() => import("./atmosphere-canvas"), { ssr: false });

/** Device capabilities do not change while the page is open. */
function subscribeNever(): () => void {
  return () => undefined;
}

export type AtmosphereProps = {
  className?: string;
  children?: ReactNode;
};

/**
 * Lagoon water and light backdrop for the hero and loading states. Always renders the static
 * gradient (no layout shift, works without JS); the animated canvas loads lazily, only when the
 * element is on screen and the device and user settings allow it.
 */
export function Atmosphere({ className, children }: AtmosphereProps) {
  const reduced = usePrefersReducedMotion();
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(false);
  const capable = useSyncExternalStore(
    subscribeNever,
    () => shouldAnimateAtmosphere(readDeviceSignals(reduced)),
    () => false,
  );

  useEffect(() => {
    const el = ref.current;
    if (!el || !capable) return;
    const observer = new IntersectionObserver(([entry]) =>
      setVisible(entry?.isIntersecting ?? false),
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [capable]);

  return (
    <div
      ref={ref}
      className={["relative overflow-hidden", className].filter(Boolean).join(" ")}
      style={{
        backgroundImage:
          "linear-gradient(180deg, var(--color-sand) 0%, color-mix(in srgb, var(--color-lagoon) 14%, var(--color-sand)) 100%)",
      }}
    >
      {capable && visible ? <AtmosphereCanvas /> : null}
      <div className="relative">{children}</div>
    </div>
  );
}
