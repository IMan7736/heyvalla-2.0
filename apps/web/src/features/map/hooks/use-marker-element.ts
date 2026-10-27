"use client";

import { Marker, type Map as MapLibreMap } from "maplibre-gl";
import { useEffect, useState } from "react";
import type { LngLat } from "../types";

interface MarkerOptions {
  anchor?: "center" | "bottom" | "top";
  /** Keeps markers above route lines but under map controls. */
  className?: string;
}

/**
 * Creates a MapLibre marker whose DOM element React can portal into. The marker lives as long
 * as the component; move it by changing `lngLat`.
 */
export function useMarkerElement(map: MapLibreMap, lngLat: LngLat, options: MarkerOptions = {}) {
  const [element] = useState(() => {
    const node = document.createElement("div");
    if (options.className) node.className = options.className;
    return node;
  });
  const [marker] = useState(() => new Marker({ element, anchor: options.anchor ?? "center" }));
  const [lng, lat] = lngLat;

  useEffect(() => {
    marker.setLngLat([lng, lat]).addTo(map);
    return () => {
      marker.remove();
    };
  }, [map, marker, lng, lat]);

  return { element, marker };
}
