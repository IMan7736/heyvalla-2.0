"use client";

import { useSyncExternalStore } from "react";

const QUERY = "(prefers-reduced-motion: reduce)";

function subscribe(callback: () => void) {
  const list = window.matchMedia(QUERY);
  list.addEventListener("change", callback);
  return () => list.removeEventListener("change", callback);
}

/** True when the visitor asked the OS for less motion. Server render assumes motion is fine. */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(QUERY).matches,
    () => false,
  );
}
