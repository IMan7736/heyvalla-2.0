"use client";

import { lazy, Suspense } from "react";
import { MapSkeleton } from "./map-skeleton";
import type { NetworkMapProps } from "./network-map-impl";

export type { NetworkConnection, NetworkMapProps } from "./network-map-impl";

const NetworkMapImpl = lazy(() => import("./network-map-impl"));

/** Exploration view: clustered islands, mode toggles, service-density glow, search and fly-to. */
export function NetworkMap(props: NetworkMapProps) {
  return (
    <Suspense fallback={<MapSkeleton className={props.className} />}>
      <NetworkMapImpl {...props} />
    </Suspense>
  );
}
