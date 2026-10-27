"use client";

import { lazy, Suspense } from "react";
import { MapSkeleton } from "./map-skeleton";
import type { MapPickerProps } from "./map-picker-impl";

export type { MapPickerProps } from "./map-picker-impl";

const MapPickerImpl = lazy(() => import("./map-picker-impl"));

/** Pick an island by clicking the map or searching by name; feeds the planner form. */
export function MapPicker(props: MapPickerProps) {
  return (
    <Suspense fallback={<MapSkeleton className={props.className} />}>
      <MapPickerImpl {...props} />
    </Suspense>
  );
}
