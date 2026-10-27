"use client";

import { useFormatter } from "next-intl";
import { useCallback } from "react";

/** Time of day in Indian/Maldives (the next-intl request config sets the zone). */
export function useClock() {
  const format = useFormatter();
  return useCallback(
    (ms: number) =>
      format.dateTime(new Date(ms), { hour: "2-digit", minute: "2-digit", hourCycle: "h23" }),
    [format],
  );
}
