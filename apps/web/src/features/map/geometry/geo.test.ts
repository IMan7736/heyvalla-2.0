import { describe, expect, it } from "vitest";
import {
  bearingDeg,
  boundsOf,
  flightArc,
  greatCircle,
  haversineKm,
  lineLengthKm,
  padBounds,
  pointAlong,
  seaRoute,
  sliceLine,
} from "./geo";
import type { LngLat } from "../types";

const male: LngLat = [73.5093, 4.1755];
const gan: LngLat = [73.1561, -0.6933];

describe("distance and bearing", () => {
  it("measures Malé to Gan at about 540 km", () => {
    expect(haversineKm(male, gan)).toBeGreaterThan(530);
    expect(haversineKm(male, gan)).toBeLessThan(545);
  });

  it("gives due east as 90 degrees and due north as 0", () => {
    expect(bearingDeg([73, 4], [74, 4])).toBeCloseTo(90, 0);
    expect(bearingDeg([73, 4], [73, 5])).toBeCloseTo(0, 5);
  });

  it("sums a polyline", () => {
    const mid: LngLat = [73.3, 1.5];
    expect(lineLengthKm([male, mid, gan])).toBeGreaterThanOrEqual(haversineKm(male, gan));
  });
});

describe("pointAlong and sliceLine", () => {
  const line: LngLat[] = [
    [73, 4],
    [73, 5],
    [74, 5],
  ];

  it("returns the endpoints at 0 and 1", () => {
    expect(pointAlong(line, 0).point).toEqual([73, 4]);
    expect(pointAlong(line, 1).point).toEqual([74, 5]);
  });

  it("clamps out-of-range fractions", () => {
    expect(pointAlong(line, -3).point).toEqual([73, 4]);
    expect(pointAlong(line, 9).point).toEqual([74, 5]);
  });

  it("reports the bearing of the current segment", () => {
    expect(pointAlong(line, 0.1).bearing).toBeCloseTo(0, 3);
    expect(pointAlong(line, 0.9).bearing).toBeGreaterThan(80);
  });

  it("slices to a prefix that ends where pointAlong says", () => {
    const part = sliceLine(line, 0.5);
    const end = part[part.length - 1]!;
    const expected = pointAlong(line, 0.5).point;
    expect(end[0]).toBeCloseTo(expected[0], 6);
    expect(end[1]).toBeCloseTo(expected[1], 6);
    expect(lineLengthKm(part)).toBeCloseTo(lineLengthKm(line) / 2, 1);
  });

  it("copes with a single point and a zero-length line", () => {
    expect(pointAlong([male], 0.5).point).toEqual(male);
    expect(pointAlong([male, male], 0.5).point).toEqual(male);
    expect(sliceLine([male], 0.5)).toEqual([male]);
  });

  it("refuses an empty line", () => {
    expect(() => pointAlong([], 0.5)).toThrow();
  });
});

describe("bounds", () => {
  it("wraps all points", () => {
    expect(boundsOf([male, gan])).toEqual([
      [73.1561, -0.6933],
      [73.5093, 4.1755],
    ]);
    expect(boundsOf([])).toBeNull();
  });

  it("widens a degenerate box to the minimum span", () => {
    const [[w, s], [e, n]] = padBounds([male, male], 0.02);
    expect(e - w).toBeCloseTo(0.02, 6);
    expect(n - s).toBeCloseTo(0.02, 6);
  });

  it("leaves a large box alone", () => {
    const box = boundsOf([male, gan])!;
    expect(padBounds(box)).toEqual(box);
  });
});

describe("illustrative route generators", () => {
  it("great circle and flight arc start and end exactly on the points", () => {
    for (const line of [greatCircle(male, gan), flightArc(male, gan)]) {
      expect(line[0]).toEqual(male);
      expect(line[line.length - 1]).toEqual(gan);
    }
  });

  it("flight arc bows away from the straight line", () => {
    const arc = flightArc(male, gan);
    const mid = arc[Math.floor(arc.length / 2)]!;
    const straightMidLng = (male[0] + gan[0]) / 2;
    expect(Math.abs(mid[0] - straightMidLng)).toBeGreaterThan(0.01);
  });

  it("sea routes are deterministic per seed and differ between seeds", () => {
    const a = seaRoute(male, [73.65, 4.28], "leg_a");
    expect(seaRoute(male, [73.65, 4.28], "leg_a")).toEqual(a);
    expect(seaRoute(male, [73.65, 4.28], "leg_b")).not.toEqual(a);
    expect(a[0]).toEqual(male);
    expect(a[a.length - 1]).toEqual([73.65, 4.28]);
  });

  it("sea routes are not longer than 1.35x the straight line", () => {
    const end: LngLat = [73.95, 4.6];
    const route = seaRoute(male, end, "leg_c");
    expect(lineLengthKm(route)).toBeLessThan(haversineKm(male, end) * 1.35);
  });

  it("returns a plain segment for a negligible hop", () => {
    expect(seaRoute(male, male, "x")).toEqual([male, male]);
  });
});
