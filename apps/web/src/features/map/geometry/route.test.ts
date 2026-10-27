import { describe, expect, it } from "vitest";
import {
  buildRoute,
  interchangeIds,
  revealAt,
  routeBounds,
  routeStops,
  traveledAt,
  vehicleAt,
} from "./route";
import type { Leg, LegFeatureCollection, PlaceRef } from "../types";

const place = (
  id: string,
  lng: number,
  lat: number,
  type: PlaceRef["type"] = "jetty",
): PlaceRef => ({
  id,
  type,
  name: { en: id },
  geo: { lat, lng },
});

const money = { amountMinor: 1000, currency: "MVR" } as const;

function leg(
  partial: Partial<Leg> & Pick<Leg, "id" | "mode" | "from" | "to" | "departAt" | "arriveAt">,
): Leg {
  return {
    providerId: "static-test",
    durationMin: 0,
    price: money,
    bookable: true,
    pricing: "fixed",
    noteCodes: [],
    ...partial,
  };
}

const ferry = leg({
  id: "leg_ferry",
  mode: "ferry",
  from: place("jty_a", 73.2, 3.9),
  to: place("jty_b", 73.5, 4.18),
  departAt: "2026-10-27T02:00:00Z",
  arriveAt: "2026-10-27T03:00:00Z",
});
const taxi = leg({
  id: "leg_taxi",
  mode: "taxi",
  from: place("jty_b", 73.5, 4.18),
  to: place("apt_mle", 73.529, 4.19, "airport"),
  departAt: "2026-10-27T03:20:00Z",
  arriveAt: "2026-10-27T03:30:00Z",
});
const flight = leg({
  id: "leg_flight",
  mode: "domestic_flight",
  from: place("apt_mle", 73.529, 4.19, "airport"),
  to: place("apt_gan", 73.156, -0.693, "airport"),
  departAt: "2026-10-27T04:30:00Z",
  arriveAt: "2026-10-27T05:30:00Z",
});
const option = { legs: [ferry, taxi, flight] };
const at = (iso: string) => Date.parse(iso);

describe("buildRoute", () => {
  it("generates flagged-approximate geometry for every leg when the server has none", () => {
    const route = buildRoute(option);
    expect(route.legs).toHaveLength(3);
    expect(route.legs.every((l) => l.approximate)).toBe(true);
    expect(route.legs[0]!.group).toBe("sea");
    expect(route.legs[1]!.coordinates).toHaveLength(2);
    expect(route.legs[2]!.coordinates.length).toBeGreaterThan(10);
    expect(route.startMs).toBe(at("2026-10-27T02:00:00Z"));
    expect(route.endMs).toBe(at("2026-10-27T05:30:00Z"));
  });

  it("prefers served geometry and keeps its approximate flag", () => {
    const served: LegFeatureCollection = {
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          properties: { legId: "leg_ferry", mode: "ferry", approximate: false },
          geometry: {
            type: "LineString",
            coordinates: [
              [73.2, 3.9],
              [73.3, 4.0],
              [73.5, 4.18],
            ],
          },
        },
      ],
    };
    const route = buildRoute(option, served);
    expect(route.legs[0]!.coordinates).toHaveLength(3);
    expect(route.legs[0]!.approximate).toBe(false);
    expect(route.legs[1]!.approximate).toBe(true);
  });

  it("resolves served geometry through geometryRef", () => {
    const withRef = { legs: [{ ...ferry, geometryRef: "geo_shared" }] };
    const served: LegFeatureCollection = {
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          properties: { legId: "geo_shared", mode: "ferry", approximate: true },
          geometry: {
            type: "LineString",
            coordinates: [
              [73.2, 3.9],
              [73.5, 4.18],
            ],
          },
        },
      ],
    };
    expect(buildRoute(withRef, served).legs[0]!.coordinates).toHaveLength(2);
  });

  it("handles an option with no legs", () => {
    const route = buildRoute({ legs: [] });
    expect(route.legs).toEqual([]);
    expect(vehicleAt(route, 0)).toBeNull();
    expect(routeBounds(route)).toBeNull();
  });
});

describe("vehicleAt", () => {
  const route = buildRoute(option);

  it("sits at the origin before departure", () => {
    const v = vehicleAt(route, at("2026-10-27T01:00:00Z"))!;
    expect(v.phase).toBe("before");
    expect(v.point).toEqual([73.2, 3.9]);
  });

  it("moves along a leg in proportion to time", () => {
    const v = vehicleAt(route, at("2026-10-27T02:30:00Z"))!;
    expect(v.phase).toBe("moving");
    expect(v.legIndex).toBe(0);
    expect(v.point[0]).toBeGreaterThan(73.2);
    expect(v.point[0]).toBeLessThan(73.5);
  });

  it("waits at the connection and reports minutes until the next departure", () => {
    const v = vehicleAt(route, at("2026-10-27T03:10:00Z"))!;
    expect(v.phase).toBe("waiting");
    expect(v.legIndex).toBe(0);
    expect(v.waitingMinutes).toBe(10);
    expect(v.point).toEqual([73.5, 4.18]);
  });

  it("ends at the destination", () => {
    const v = vehicleAt(route, at("2026-10-27T09:00:00Z"))!;
    expect(v.phase).toBe("after");
    expect(v.point).toEqual([73.156, -0.693]);
  });

  it("treats a zero-length leg without dividing by zero", () => {
    const instant = buildRoute({ legs: [{ ...taxi, arriveAt: taxi.departAt }] });
    const v = vehicleAt(instant, at("2026-10-27T03:20:00Z"));
    expect(Number.isFinite(v!.point[0])).toBe(true);
  });
});

describe("traveledAt and revealAt", () => {
  const route = buildRoute(option);

  it("reports untouched, partial and finished legs", () => {
    const parts = traveledAt(route, at("2026-10-27T03:15:00Z"));
    expect(parts[0]).toEqual(route.legs[0]!.coordinates);
    expect(parts[1]).toEqual([]);
    const mid = traveledAt(route, at("2026-10-27T04:00:00Z"));
    expect(mid[1]).toEqual(route.legs[1]!.coordinates);
    const partial = traveledAt(route, at("2026-10-27T05:00:00Z"));
    expect(partial[2]!.length).toBeGreaterThan(1);
    expect(partial[2]).not.toEqual(route.legs[2]!.coordinates);
  });

  it("reveals nothing at 0 and everything at 1", () => {
    expect(revealAt(route, 0).every((p) => p.length === 0)).toBe(true);
    expect(revealAt(route, 1)).toEqual(route.legs.map((l) => l.coordinates));
  });

  it("reveals legs in order", () => {
    const parts = revealAt(route, 0.01);
    expect(parts[0]!.length).toBeGreaterThan(0);
    expect(parts[2]).toEqual([]);
  });
});

describe("misc", () => {
  const route = buildRoute(option);

  it("finds interchanges between legs", () => {
    expect([...interchangeIds(route)].sort()).toEqual(["apt_mle", "jty_b"]);
  });

  it("returns bounds that contain every stop", () => {
    const [[w, s], [e, n]] = routeBounds(route)!;
    expect(w).toBeLessThanOrEqual(73.156);
    expect(e).toBeGreaterThanOrEqual(73.529);
    expect(s).toBeLessThanOrEqual(-0.693);
    expect(n).toBeGreaterThanOrEqual(4.19);
  });
});

describe("routeStops", () => {
  it("merges the end of one leg and the start of the next into one interchange", () => {
    const stops = routeStops(buildRoute(option));
    expect(stops.map((s) => [s.place.id, s.kind, s.number])).toEqual([
      ["jty_a", "start", 1],
      ["jty_b", "interchange", 2],
      ["apt_mle", "interchange", 3],
      ["apt_gan", "end", 4],
    ]);
    expect(stops[1]!.arriveMs).toBe(at("2026-10-27T03:00:00Z"));
    expect(stops[1]!.departMs).toBe(at("2026-10-27T03:20:00Z"));
  });

  it("keeps separate stops when consecutive legs do not share a place", () => {
    const gap = { legs: [ferry, { ...flight, id: "leg_gap" }] };
    expect(routeStops(buildRoute(gap)).map((s) => s.number)).toEqual([1, 2, 3, 4]);
  });

  it("is empty without legs", () => {
    expect(routeStops(buildRoute({ legs: [] }))).toEqual([]);
  });
});
