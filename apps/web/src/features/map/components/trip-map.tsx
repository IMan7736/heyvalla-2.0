"use client";

import { lazy, Suspense } from "react";
import { MapSkeleton } from "./map-skeleton";
import type { TripMapProps } from "./trip-map-impl";

export type { TripMapHandle, TripMapProps } from "./trip-map-impl";

// The WebGL bundle (maplibre-gl, pmtiles) is only fetched when a map is actually shown.
const TripMapImpl = lazy(() => import("./trip-map-impl"));

/**
 * The journey map: draws a TripOption's legs (dashed sea lanes, coral flight arcs, road lines),
 * animates the route in, and moves a vehicle along it in sync with a timeline scrubber.
 */
export function TripMap(props: TripMapProps) {
  return (
    <Suspense fallback={<MapSkeleton className={props.className} />}>
      <TripMapImpl {...props} />
    </Suspense>
  );
}
