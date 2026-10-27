"use client";

import type { Map as MapLibreMap, MapMouseEvent } from "maplibre-gl";
import { useTranslations } from "next-intl";
import {
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type Ref,
} from "react";
import {
  buildRoute,
  legBounds,
  revealAt,
  routeBounds,
  routeStops,
  traveledAt,
  vehicleAt,
  type Route,
  type RouteStop,
} from "../geometry/route";
import { useMapFocus, type MapFocus } from "../hooks/use-map-focus";
import { usePrefersReducedMotion } from "../hooks/use-reduced-motion";
import { useTimeline } from "../hooks/use-timeline";
import { MALDIVES_BOUNDS } from "../config";
import { ROUTE_LAYERS } from "../style/layer-ids";
import {
  addRouteLayers,
  legFeatures,
  removeRouteLayers,
  setRouteSource,
} from "../style/route-layers";
import type { LegFeatureCollection, TripOption } from "../types";
import { FitIcon } from "./icons";
import { useClock } from "./format";
import { MapCanvas, useMap, type MapPadding } from "./map-canvas";
import styles from "./map.module.css";
import { RouteList } from "./route-list";
import { Scrubber } from "./scrubber";
import { StopMarker } from "./stop-marker";
import { VehicleMarker } from "./vehicle-marker";

/** Imperative controls, for parents that drive the map from a timeline list. */
export interface TripMapHandle {
  focusLeg: (legId: string | null) => void;
  fitRoute: () => void;
  play: () => void;
  pause: () => void;
  seek: (timeMs: number) => void;
}

export interface TripMapProps {
  option: Pick<TripOption, "legs">;
  /** Geometry from GET /geo/legs. Legs without it get an illustrative, flagged shape. */
  geometries?: LegFeatureCollection;
  /** Shared focus state (see useMapFocus) so a list and the map highlight the same leg. */
  focus?: MapFocus;
  /** Controlled journey time (epoch ms). */
  time?: number;
  onTimeChange?: (timeMs: number) => void;
  autoPlay?: boolean;
  showScrubber?: boolean;
  padding?: Partial<MapPadding>;
  className?: string;
  ref?: Ref<TripMapHandle>;
}

const easeInOut = (t: number) => (t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2);
const INTRO_MS = 1300;
const ROUTE_LINE_LAYERS = [ROUTE_LAYERS.ahead].flatMap((base) =>
  ["sea", "air", "road", "walk"].map((g) => `${base}-${g}`),
);

interface SceneProps extends Pick<
  TripMapProps,
  "autoPlay" | "showScrubber" | "time" | "onTimeChange"
> {
  route: Route;
  focus: MapFocus;
  padding: MapPadding;
  handleRef?: Ref<TripMapHandle>;
}

function TripScene({
  route,
  focus,
  padding,
  autoPlay,
  showScrubber = true,
  time,
  onTimeChange,
  handleRef,
}: SceneProps) {
  const map = useMap() as MapLibreMap;
  const t = useTranslations("map");
  const reduceMotion = usePrefersReducedMotion();
  const timeline = useTimeline({ route, time, onTimeChange, autoPlay: autoPlay && !reduceMotion });
  const stops = useMemo(() => routeStops(route), [route]);
  const vehicle = useMemo(() => vehicleAt(route, timeline.timeMs), [route, timeline.timeMs]);
  const [introRoute, setIntroRoute] = useState<Route | null>(null);
  const introDone = reduceMotion || introRoute === route;
  const { focusedLegId, hoveredLegId, focusLeg, hoverLeg } = focus;
  const bounds = useMemo(() => routeBounds(route), [route]);
  const hasApproximate = route.legs.some((l) => l.approximate);

  const fitRoute = useCallback(() => {
    if (bounds) map.fitBounds(bounds, { padding, duration: reduceMotion ? 0 : 900, maxZoom: 14 });
  }, [map, bounds, padding, reduceMotion]);

  useEffect(() => {
    addRouteLayers(map);
    return () => removeRouteLayers(map);
  }, [map]);

  // Intro: draw the journey along its length (instantly when motion is reduced).
  useEffect(() => {
    if (reduceMotion) {
      setRouteSource(map, "ahead", legFeatures(route, revealAt(route, 1)));
      return;
    }
    let frame = 0;
    const begin = performance.now();
    const step = (now: number) => {
      const progress = Math.min(1, (now - begin) / INTRO_MS);
      setRouteSource(map, "ahead", legFeatures(route, revealAt(route, easeInOut(progress))));
      if (progress < 1) frame = requestAnimationFrame(step);
      else setIntroRoute(route);
    };
    frame = requestAnimationFrame(step);
    return () => cancelAnimationFrame(frame);
  }, [map, route, reduceMotion]);

  useEffect(() => {
    setRouteSource(map, "done", legFeatures(route, traveledAt(route, timeline.timeMs)));
  }, [map, route, timeline.timeMs]);

  // Camera follows the route and, when set, the focused leg.
  const firstFit = useRef(true);
  useEffect(() => {
    if (focusedLegId) {
      const leg = route.legs.find((l) => l.id === focusedLegId);
      if (leg)
        map.fitBounds(legBounds(leg), { padding, duration: reduceMotion ? 0 : 800, maxZoom: 15 });
      return;
    }
    if (firstFit.current) {
      // The container may have been resized after creation, so frame the route again, instantly.
      firstFit.current = false;
      map.resize();
      if (bounds) map.fitBounds(bounds, { padding, duration: 0, maxZoom: 14 });
      return;
    }
    fitRoute();
  }, [map, route, focusedLegId, padding, reduceMotion, fitRoute, bounds]);

  useEffect(() => {
    const highlighted = focusedLegId ?? hoveredLegId;
    setRouteSource(
      map,
      "focus",
      highlighted
        ? legFeatures(
            route,
            route.legs.map((l) => l.coordinates),
            highlighted,
          )
        : legFeatures(route, []),
    );
  }, [map, route, focusedLegId, hoveredLegId]);

  // Clicking or hovering a leg on the map.
  useEffect(() => {
    const legIdAt = (event: MapMouseEvent) => {
      const hit = map.queryRenderedFeatures(event.point, {
        layers: ROUTE_LINE_LAYERS.filter((id) => map.getLayer(id)),
      })[0];
      return (hit?.properties?.legId as string | undefined) ?? null;
    };
    const onClick = (event: MapMouseEvent) => {
      const id = legIdAt(event);
      if (id) focusLeg(id === focusedLegId ? null : id);
    };
    const onMove = (event: MapMouseEvent) => {
      const id = legIdAt(event);
      map.getCanvas().style.cursor = id ? "pointer" : "";
      hoverLeg(id);
    };
    map.on("click", onClick);
    map.on("mousemove", onMove);
    return () => {
      map.off("click", onClick);
      map.off("mousemove", onMove);
      map.getCanvas().style.cursor = "";
    };
  }, [map, focusLeg, hoverLeg, focusedLegId]);

  useImperativeHandle(
    handleRef,
    () => ({
      focusLeg,
      fitRoute: () => {
        focusLeg(null);
        fitRoute();
      },
      play: timeline.play,
      pause: timeline.pause,
      seek: timeline.seek,
    }),
    [focusLeg, fitRoute, timeline.play, timeline.pause, timeline.seek],
  );

  const activeLeg = focusedLegId ?? hoveredLegId;
  const isActiveStop = (stop: RouteStop) => {
    const leg = activeLeg ? route.legs.find((l) => l.id === activeLeg) : undefined;
    return Boolean(leg && (leg.from.id === stop.place.id || leg.to.id === stop.place.id));
  };
  const vehicleLeg = vehicle ? route.legs[vehicle.legIndex] : undefined;

  return (
    <>
      <div className={styles.topStart}>
        <button
          type="button"
          className={styles.fitButton}
          onClick={() => {
            focusLeg(null);
            fitRoute();
          }}
        >
          <FitIcon /> {t("controls.fitRoute")}
        </button>
        {hasApproximate ? <p className={styles.note}>{t("approximate.note")}</p> : null}
      </div>
      {stops.map((stop) => (
        <StopMarker
          key={stop.key}
          map={map}
          stop={stop}
          active={isActiveStop(stop)}
          onActivate={(s) => focusLeg(route.legs[s.legIndex]?.id ?? null)}
        />
      ))}
      {introDone && vehicle && vehicleLeg ? (
        <VehicleMarker map={map} mode={vehicleLeg.mode} state={vehicle} />
      ) : null}
      {showScrubber && route.endMs > route.startMs ? (
        <Scrubber route={route} timeline={timeline} vehicle={vehicle} />
      ) : null}
    </>
  );
}

const DEFAULT_PADDING: MapPadding = { top: 96, right: 80, bottom: 80, left: 80 };

export default function TripMapImpl({
  option,
  geometries,
  focus,
  time,
  onTimeChange,
  autoPlay = false,
  showScrubber = true,
  padding,
  className,
  ref,
}: TripMapProps) {
  const t = useTranslations("map");
  const clock = useClock();
  const route = useMemo(() => buildRoute(option, geometries), [option, geometries]);
  const ownFocus = useMapFocus();
  const activeFocus = focus ?? ownFocus;
  const [unavailable, setUnavailable] = useState(false);
  const fullPadding = useMemo<MapPadding>(
    () => ({ ...DEFAULT_PADDING, bottom: showScrubber ? 170 : DEFAULT_PADDING.bottom, ...padding }),
    [padding, showScrubber],
  );
  const first = route.legs[0];
  const last = route.legs[route.legs.length - 1];

  const summary =
    first && last
      ? t("trip.summary", {
          legs: route.legs.length,
          from: first.from.name.en,
          to: last.to.name.en,
          depart: clock(route.startMs),
          arrive: clock(route.endMs),
        })
      : "";

  if (unavailable) {
    return (
      <section className={styles.fallback} aria-label={t("trip.label")} data-testid="map-fallback">
        <h3 className={styles.fallbackTitle}>{t("unavailable.title")}</h3>
        <p className={styles.fallbackBody}>{t("unavailable.body")}</p>
        {summary ? <p className={styles.fallbackBody}>{summary}</p> : null}
        <RouteList
          route={route}
          focusedLegId={activeFocus.focusedLegId}
          onFocusLeg={activeFocus.focusLeg}
        />
      </section>
    );
  }

  return (
    <div
      className={className}
      style={{ position: "relative", blockSize: "100%", inlineSize: "100%" }}
    >
      <MapCanvas
        label={t("trip.label")}
        initialBounds={routeBounds(route) ?? MALDIVES_BOUNDS}
        padding={fullPadding}
        onUnavailable={() => setUnavailable(true)}
      >
        <TripScene
          route={route}
          focus={activeFocus}
          padding={fullPadding}
          autoPlay={autoPlay}
          showScrubber={showScrubber}
          time={time}
          onTimeChange={onTimeChange}
          handleRef={ref}
        />
      </MapCanvas>
      <p className={styles.srOnly}>{summary}</p>
      <RouteList
        route={route}
        hidden
        focusedLegId={activeFocus.focusedLegId}
        onFocusLeg={activeFocus.focusLeg}
      />
    </div>
  );
}
