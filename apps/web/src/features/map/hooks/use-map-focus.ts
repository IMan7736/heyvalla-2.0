"use client";

import { useCallback, useMemo, useState } from "react";

export interface MapFocus {
  /** The leg the camera is centred on (set by clicking a leg in a list or on the map). */
  focusedLegId: string | null;
  /** The leg under the pointer, for hover highlighting both ways. */
  hoveredLegId: string | null;
  focusLeg: (legId: string | null) => void;
  hoverLeg: (legId: string | null) => void;
}

/**
 * Shared focus state so a timeline list and the map highlight the same leg.
 * Own it in the parent and pass it to `<TripMap focus={focus} />`.
 */
export function useMapFocus(initialLegId: string | null = null): MapFocus {
  const [focusedLegId, setFocused] = useState<string | null>(initialLegId);
  const [hoveredLegId, setHovered] = useState<string | null>(null);
  const focusLeg = useCallback((legId: string | null) => setFocused(legId), []);
  const hoverLeg = useCallback((legId: string | null) => setHovered(legId), []);
  return useMemo(
    () => ({ focusedLegId, hoveredLegId, focusLeg, hoverLeg }),
    [focusedLegId, hoveredLegId, focusLeg, hoverLeg],
  );
}
