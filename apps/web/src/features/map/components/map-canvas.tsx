"use client";

import "maplibre-gl/dist/maplibre-gl.css";
import * as maplibregl from "maplibre-gl";
import type { Map as MapLibreMap } from "maplibre-gl";
import { useTranslations } from "next-intl";
import { Protocol } from "pmtiles";
import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { MALDIVES_BOUNDS, MAX_ZOOM, MIN_ZOOM, tilesBaseUrl } from "../config";
import { usePrefersReducedMotion } from "../hooks/use-reduced-motion";
import { createMapStyle } from "../style/create-style";
import { registerMapIcons } from "../style/icons";
import type { Bounds } from "../types";
import styles from "./map.module.css";

let protocolRegistered = false;
function ensurePmtilesProtocol() {
  if (protocolRegistered) return;
  maplibregl.addProtocol("pmtiles", new Protocol().tile);
  protocolRegistered = true;
}

export function webglAvailable(): boolean {
  try {
    const canvas = document.createElement("canvas");
    return Boolean(canvas.getContext("webgl2") ?? canvas.getContext("webgl"));
  } catch {
    return false;
  }
}

const MapContext = createContext<MapLibreMap | null>(null);

/** The MapLibre instance of the nearest <MapCanvas>, or null until the style has loaded. */
export function useMap(): MapLibreMap | null {
  return useContext(MapContext);
}

export interface MapPadding {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface MapCanvasProps {
  /** Accessible name for the map region. */
  label: string;
  initialBounds?: Bounds;
  padding?: MapPadding;
  className?: string;
  /** Rendered over the map once the style is ready (markers, controls). */
  children?: ReactNode;
  /** Called instead of rendering the map when WebGL or the style cannot start. */
  onUnavailable?: () => void;
  onMapReady?: (map: MapLibreMap) => void;
}

const DEFAULT_PADDING: MapPadding = { top: 72, right: 72, bottom: 72, left: 72 };

/**
 * Owns one MapLibre instance with the Dhathuru style. Everything else in the feature renders
 * inside it as children and reaches the map through `useMap()`.
 */
export function MapCanvas({
  label,
  initialBounds = MALDIVES_BOUNDS,
  padding = DEFAULT_PADDING,
  className,
  children,
  onUnavailable,
  onMapReady,
}: MapCanvasProps) {
  const t = useTranslations("map");
  const container = useRef<HTMLDivElement>(null);
  const [map, setMap] = useState<MapLibreMap | null>(null);
  const reduceMotion = usePrefersReducedMotion();
  const callbacks = useRef({ onUnavailable, onMapReady });
  const initial = useRef({ initialBounds, padding });

  useEffect(() => {
    callbacks.current = { onUnavailable, onMapReady };
  });

  useEffect(() => {
    const node = container.current;
    if (!node) return;
    if (!webglAvailable()) {
      callbacks.current.onUnavailable?.();
      return;
    }
    ensurePmtilesProtocol();

    let instance: MapLibreMap;
    try {
      instance = new maplibregl.Map({
        container: node,
        style: createMapStyle({ baseUrl: tilesBaseUrl() }),
        bounds: initial.current.initialBounds,
        fitBoundsOptions: { padding: initial.current.padding, animate: false },
        minZoom: MIN_ZOOM,
        maxZoom: MAX_ZOOM,
        maxBounds: [
          [MALDIVES_BOUNDS[0][0] - 6, MALDIVES_BOUNDS[0][1] - 6],
          [MALDIVES_BOUNDS[1][0] + 6, MALDIVES_BOUNDS[1][1] + 6],
        ],
        attributionControl: false,
        dragRotate: false,
        pitchWithRotate: false,
        touchPitch: false,
        fadeDuration: 150,
        canvasContextAttributes: { antialias: true },
      });
    } catch {
      callbacks.current.onUnavailable?.();
      return;
    }
    instance.touchZoomRotate.disableRotation();
    instance.keyboard.disableRotation();
    instance.addControl(new maplibregl.AttributionControl({ compact: true }), "bottom-right");
    instance.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");
    instance.addControl(
      new maplibregl.ScaleControl({ unit: "metric", maxWidth: 110 }),
      "bottom-left",
    );

    let disposed = false;
    instance.once("load", async () => {
      await registerMapIcons(instance);
      if (disposed) return;
      setMap(instance);
      callbacks.current.onMapReady?.(instance);
    });
    instance.on("error", (event) => {
      // A missing tile server surfaces here; the map stays blank but the page keeps working.
      if (event.error?.message?.includes("Failed to fetch") && !instance.loaded()) {
        callbacks.current.onUnavailable?.();
      }
    });

    return () => {
      disposed = true;
      setMap(null);
      instance.remove();
    };
  }, []);

  useEffect(() => {
    // Keep the map crisp when the layout around it changes size.
    const node = container.current;
    if (!map || !node) return;
    const observer = new ResizeObserver(() => map.resize());
    observer.observe(node);
    return () => observer.disconnect();
  }, [map]);

  return (
    <div
      className={[styles.canvasRoot, className].filter(Boolean).join(" ")}
      data-reduced-motion={reduceMotion ? "true" : "false"}
    >
      <div
        ref={container}
        className={styles.canvas}
        role="region"
        aria-label={label}
        data-map-ready={map ? "true" : "false"}
      />
      {map ? null : (
        <div className={styles.skeleton} role="status" aria-live="polite">
          <span className={styles.skeletonShimmer} aria-hidden="true" />
          <span className={styles.srOnly}>{t("loading")}</span>
        </div>
      )}
      <MapContext value={map}>{map ? children : null}</MapContext>
    </div>
  );
}
