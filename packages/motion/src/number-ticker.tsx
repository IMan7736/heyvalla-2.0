"use client";

import { animate, useMotionValue, useMotionValueEvent } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { duration, ease } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type NumberTickerProps = {
  value: number;
  /** Formats the displayed number (use Intl for money, minor units in, major out). */
  format?: (value: number) => string;
  className?: string;
};

const defaultFormat = (n: number): string => Math.round(n).toString();

/**
 * Counts to a new value. The accessible text always shows the final value; the animated
 * digits are aria-hidden so screen readers do not hear every intermediate number.
 */
export function NumberTicker({ value, format = defaultFormat, className }: NumberTickerProps) {
  const reduced = usePrefersReducedMotion();
  const motionValue = useMotionValue(value);
  const [display, setDisplay] = useState(() => format(value));
  const formatRef = useRef(format);
  formatRef.current = format;

  useMotionValueEvent(motionValue, "change", (latest) => setDisplay(formatRef.current(latest)));

  useEffect(() => {
    if (reduced) {
      motionValue.set(value);
      setDisplay(formatRef.current(value));
      return;
    }
    const controls = animate(motionValue, value, { duration: duration.slow, ease: ease.tide });
    return () => controls.stop();
  }, [value, reduced, motionValue]);

  return (
    <span className={className}>
      <span className="sr-only">{format(value)}</span>
      <span aria-hidden="true" style={{ fontVariantNumeric: "tabular-nums" }}>
        {display}
      </span>
    </span>
  );
}
