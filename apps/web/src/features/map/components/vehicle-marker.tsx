"use client";

import type { Map as MapLibreMap } from "maplibre-gl";
import { createPortal } from "react-dom";
import type { VehicleState } from "../geometry/route";
import { useMarkerElement } from "../hooks/use-marker-element";
import type { TransportMode } from "../types";
import { ModeIcon } from "./icons";
import styles from "./map.module.css";

interface VehicleMarkerProps {
  map: MapLibreMap;
  mode: TransportMode;
  state: VehicleState;
}

/** The moving traveller. Decorative: the scrubber announces the same information in words. */
export function VehicleMarker({ map, mode, state }: VehicleMarkerProps) {
  const { element } = useMarkerElement(map, state.point, { className: styles.marker });
  // Boats and cars are drawn pointing up, planes point up too; rotate to the heading of travel.
  const rotates = mode === "domestic_flight" || mode === "seaplane";
  return createPortal(
    <div
      className={styles.vehicle}
      aria-hidden="true"
      data-testid="map-vehicle"
      data-phase={state.phase}
    >
      <span
        className={styles.vehicleIcon}
        style={rotates ? { rotate: `${state.bearing}deg` } : undefined}
      >
        <ModeIcon mode={mode} size={18} />
      </span>
    </div>,
    element,
  );
}
