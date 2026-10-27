import {
  bearingDeg,
  boundsOf,
  flightArc,
  lineLengthKm,
  padBounds,
  pointAlong,
  seaRoute,
  sliceLine,
} from "./geo";
import type {
  Bounds,
  Leg,
  LegFeatureCollection,
  LngLat,
  ModeGroup,
  TransportMode,
  TripOption,
} from "../types";

export const MODE_GROUP: Record<TransportMode, ModeGroup> = {
  ferry: "sea",
  speedboat: "sea",
  resort_transfer: "sea",
  domestic_flight: "air",
  seaplane: "air",
  taxi: "road",
  bus: "road",
  walk: "walk",
};

export interface RouteLeg {
  id: string;
  index: number;
  mode: TransportMode;
  group: ModeGroup;
  from: Leg["from"];
  to: Leg["to"];
  departMs: number;
  arriveMs: number;
  coordinates: LngLat[];
  lengthKm: number;
  /** True for any geometry we invented or that the data source flagged as approximate. */
  approximate: boolean;
}

export interface Route {
  legs: RouteLeg[];
  startMs: number;
  endMs: number;
  totalKm: number;
}

const toLngLat = (p: { lng: number; lat: number }): LngLat => [p.lng, p.lat];

/** Illustrative geometry for a leg when the server has none. */
export function generateLegCoordinates(leg: Pick<Leg, "id" | "mode" | "from" | "to">): LngLat[] {
  const a = toLngLat(leg.from.geo);
  const b = toLngLat(leg.to.geo);
  switch (MODE_GROUP[leg.mode]) {
    case "sea":
      return seaRoute(a, b, leg.id);
    case "air":
      return flightArc(a, b, 48, leg.mode === "seaplane" ? 0.08 : 0.18);
    default:
      return [a, b];
  }
}

function indexGeometries(collection?: LegFeatureCollection) {
  const byLeg = new Map<string, { coordinates: LngLat[]; approximate: boolean }>();
  for (const feature of collection?.features ?? []) {
    const coordinates = feature.geometry.coordinates
      .filter((c): c is number[] => c.length >= 2)
      .map((c): LngLat => [c[0]!, c[1]!]);
    if (coordinates.length >= 2) {
      byLeg.set(feature.properties.legId, {
        coordinates,
        approximate: feature.properties.approximate,
      });
    }
  }
  return byLeg;
}

/**
 * Turns a trip option into drawable legs. Server geometry (GET /geo/legs) wins; any leg
 * without it gets an illustrative generated shape and is flagged approximate.
 */
export function buildRoute(
  option: Pick<TripOption, "legs">,
  geometries?: LegFeatureCollection,
): Route {
  const served = indexGeometries(geometries);
  const legs = option.legs.map((leg, index): RouteLeg => {
    const fromServer =
      served.get(leg.id) ?? (leg.geometryRef ? served.get(leg.geometryRef) : undefined);
    const coordinates = fromServer?.coordinates ?? generateLegCoordinates(leg);
    return {
      id: leg.id,
      index,
      mode: leg.mode,
      group: MODE_GROUP[leg.mode],
      from: leg.from,
      to: leg.to,
      departMs: Date.parse(leg.departAt),
      arriveMs: Date.parse(leg.arriveAt),
      coordinates,
      lengthKm: lineLengthKm(coordinates),
      approximate: fromServer?.approximate ?? true,
    };
  });
  return {
    legs,
    startMs: legs.length ? Math.min(...legs.map((l) => l.departMs)) : 0,
    endMs: legs.length ? Math.max(...legs.map((l) => l.arriveMs)) : 0,
    totalKm: legs.reduce((sum, l) => sum + l.lengthKm, 0),
  };
}

export function routeBounds(route: Route, minSpanDeg = 0.02): Bounds | null {
  const bounds = boundsOf(route.legs.flatMap((l) => l.coordinates));
  return bounds ? padBounds(bounds, minSpanDeg) : null;
}

export function legBounds(leg: RouteLeg, minSpanDeg = 0.02): Bounds {
  return padBounds(
    boundsOf(leg.coordinates) ?? [leg.coordinates[0]!, leg.coordinates[0]!],
    minSpanDeg,
  );
}

export type VehiclePhase = "before" | "moving" | "waiting" | "after";

export interface VehicleState {
  phase: VehiclePhase;
  point: LngLat;
  bearing: number;
  /** Index of the leg being travelled, or the leg just finished while waiting. */
  legIndex: number;
  /** While waiting: minutes until the next departure. */
  waitingMinutes?: number;
}

/** Where the traveller is at `timeMs`: moving along a leg, waiting at a connection, or at either end. */
export function vehicleAt(route: Route, timeMs: number): VehicleState | null {
  const { legs } = route;
  const first = legs[0];
  const last = legs[legs.length - 1];
  if (!first || !last) return null;

  if (timeMs <= first.departMs) {
    return {
      phase: "before",
      point: first.coordinates[0]!,
      bearing: headingOf(first, 0),
      legIndex: 0,
    };
  }
  for (const leg of legs) {
    if (timeMs < leg.arriveMs) {
      const span = Math.max(1, leg.arriveMs - leg.departMs);
      const fraction = timeMs <= leg.departMs ? 0 : (timeMs - leg.departMs) / span;
      const { point, bearing } = pointAlong(leg.coordinates, fraction);
      return { phase: "moving", point, bearing, legIndex: leg.index };
    }
    const next = legs[leg.index + 1];
    if (next && timeMs < next.departMs) {
      return {
        phase: "waiting",
        point: leg.coordinates[leg.coordinates.length - 1]!,
        bearing: headingOf(leg, 1),
        legIndex: leg.index,
        waitingMinutes: Math.round((next.departMs - timeMs) / 60_000),
      };
    }
  }
  return {
    phase: "after",
    point: last.coordinates[last.coordinates.length - 1]!,
    bearing: headingOf(last, 1),
    legIndex: last.index,
  };
}

function headingOf(leg: RouteLeg, at: 0 | 1): number {
  const c = leg.coordinates;
  if (c.length < 2) return 0;
  return at === 0 ? bearingDeg(c[0]!, c[1]!) : bearingDeg(c[c.length - 2]!, c[c.length - 1]!);
}

/** The portion of each leg already travelled at `timeMs` (empty array for untouched legs). */
export function traveledAt(route: Route, timeMs: number): LngLat[][] {
  return route.legs.map((leg) => {
    if (timeMs <= leg.departMs) return [];
    if (timeMs >= leg.arriveMs) return leg.coordinates;
    return sliceLine(
      leg.coordinates,
      (timeMs - leg.departMs) / Math.max(1, leg.arriveMs - leg.departMs),
    );
  });
}

/** For the intro "draw" animation: each leg clipped so the whole route is revealed by distance. */
export function revealAt(route: Route, fraction: number): LngLat[][] {
  if (fraction >= 1) return route.legs.map((leg) => leg.coordinates);
  let remaining = route.totalKm * Math.max(0, fraction);
  return route.legs.map((leg) => {
    if (remaining <= 0) return [];
    if (remaining >= leg.lengthKm) {
      remaining -= leg.lengthKm;
      return leg.coordinates;
    }
    const part = sliceLine(leg.coordinates, remaining / leg.lengthKm);
    remaining = 0;
    return part;
  });
}

/** Places where one leg ends and the next begins, i.e. interchanges worth marking as hubs. */
export function interchangeIds(route: Route): Set<string> {
  const ids = new Set<string>();
  for (const leg of route.legs) {
    const next = route.legs[leg.index + 1];
    if (next) ids.add(leg.to.id);
  }
  return ids;
}

export interface RouteStop {
  key: string;
  place: Leg["from"];
  /** 1-based position along the journey. */
  number: number;
  kind: "start" | "interchange" | "end";
  arriveMs?: number;
  departMs?: number;
  /** Index of the leg that starts here (or the last leg for the destination). */
  legIndex: number;
}

/**
 * The places along a journey in order. A place where one leg ends and the next begins (even
 * across a short walk between two places with the same id) is a single interchange stop.
 */
export function routeStops(route: Route): RouteStop[] {
  const stops: RouteStop[] = [];
  route.legs.forEach((leg, i) => {
    const previous = route.legs[i - 1];
    if (!previous) {
      stops.push({
        key: `${leg.id}:from`,
        place: leg.from,
        number: 1,
        kind: "start",
        departMs: leg.departMs,
        legIndex: i,
      });
    } else if (previous.to.id === leg.from.id) {
      const joined = stops[stops.length - 1]!;
      joined.kind = "interchange";
      joined.departMs = leg.departMs;
      joined.legIndex = i;
    } else {
      stops.push({
        key: `${leg.id}:from`,
        place: leg.from,
        number: stops.length + 1,
        kind: "interchange",
        departMs: leg.departMs,
        legIndex: i,
      });
    }
    stops.push({
      key: `${leg.id}:to`,
      place: leg.to,
      number: stops.length + 1,
      kind: i === route.legs.length - 1 ? "end" : "interchange",
      arriveMs: leg.arriveMs,
      legIndex: i,
    });
  });
  return stops;
}
