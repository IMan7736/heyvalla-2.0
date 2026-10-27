import type { Bounds, LngLat } from "../types";

const EARTH_RADIUS_KM = 6371.0088;
const toRad = (deg: number) => (deg * Math.PI) / 180;
const toDeg = (rad: number) => (rad * 180) / Math.PI;

export function haversineKm(a: LngLat, b: LngLat): number {
  const dLat = toRad(b[1] - a[1]);
  const dLng = toRad(b[0] - a[0]);
  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(a[1])) * Math.cos(toRad(b[1])) * Math.sin(dLng / 2) ** 2;
  return 2 * EARTH_RADIUS_KM * Math.asin(Math.min(1, Math.sqrt(h)));
}

/** Initial compass bearing from a to b in degrees, 0 = north, clockwise. */
export function bearingDeg(a: LngLat, b: LngLat): number {
  const dLng = toRad(b[0] - a[0]);
  const y = Math.sin(dLng) * Math.cos(toRad(b[1]));
  const x =
    Math.cos(toRad(a[1])) * Math.sin(toRad(b[1])) -
    Math.sin(toRad(a[1])) * Math.cos(toRad(b[1])) * Math.cos(dLng);
  return (toDeg(Math.atan2(y, x)) + 360) % 360;
}

export function lineLengthKm(line: readonly LngLat[]): number {
  let total = 0;
  for (let i = 1; i < line.length; i++) total += haversineKm(line[i - 1]!, line[i]!);
  return total;
}

export interface PointOnLine {
  point: LngLat;
  bearing: number;
}

const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

/** Point at `fraction` (0..1) of the line's length, with the local bearing. */
export function pointAlong(line: readonly LngLat[], fraction: number): PointOnLine {
  if (line.length === 0) throw new Error("pointAlong needs at least one coordinate");
  const first = line[0]!;
  if (line.length === 1) return { point: first, bearing: 0 };
  const f = Math.min(1, Math.max(0, fraction));
  const target = lineLengthKm(line) * f;
  let walked = 0;
  for (let i = 1; i < line.length; i++) {
    const a = line[i - 1]!;
    const b = line[i]!;
    const seg = haversineKm(a, b);
    if (walked + seg >= target || i === line.length - 1) {
      const t = seg === 0 ? 0 : Math.min(1, (target - walked) / seg);
      return { point: [lerp(a[0], b[0], t), lerp(a[1], b[1], t)], bearing: bearingDeg(a, b) };
    }
    walked += seg;
  }
  return { point: line[line.length - 1]!, bearing: 0 };
}

/** The part of the line from the start up to `fraction` of its length. */
export function sliceLine(line: readonly LngLat[], fraction: number): LngLat[] {
  if (line.length < 2) return [...line];
  const f = Math.min(1, Math.max(0, fraction));
  if (f >= 1) return [...line];
  const target = lineLengthKm(line) * f;
  const out: LngLat[] = [line[0]!];
  let walked = 0;
  for (let i = 1; i < line.length; i++) {
    const a = line[i - 1]!;
    const b = line[i]!;
    const seg = haversineKm(a, b);
    if (walked + seg >= target) {
      const t = seg === 0 ? 0 : (target - walked) / seg;
      out.push([lerp(a[0], b[0], t), lerp(a[1], b[1], t)]);
      return out;
    }
    out.push(b);
    walked += seg;
  }
  return out;
}

export function boundsOf(points: readonly LngLat[]): Bounds | null {
  if (points.length === 0) return null;
  let west = Infinity;
  let south = Infinity;
  let east = -Infinity;
  let north = -Infinity;
  for (const [lng, lat] of points) {
    west = Math.min(west, lng);
    east = Math.max(east, lng);
    south = Math.min(south, lat);
    north = Math.max(north, lat);
  }
  return [
    [west, south],
    [east, north],
  ];
}

/**
 * Widen degenerate bounds (a single point, or a very short hop) so the camera never zooms
 * into a few metres. `minSpanDeg` is the smallest allowed width/height.
 */
export function padBounds(bounds: Bounds, minSpanDeg = 0.02): Bounds {
  const [[w, s], [e, n]] = bounds;
  const lngPad = Math.max(0, (minSpanDeg - (e - w)) / 2);
  const latPad = Math.max(0, (minSpanDeg - (n - s)) / 2);
  return [
    [w - lngPad, s - latPad],
    [e + lngPad, n + latPad],
  ];
}

/** Great-circle interpolation between two points (slerp), `steps` segments. */
export function greatCircle(a: LngLat, b: LngLat, steps = 48): LngLat[] {
  const [lng1, lat1] = [toRad(a[0]), toRad(a[1])];
  const [lng2, lat2] = [toRad(b[0]), toRad(b[1])];
  const d =
    2 *
    Math.asin(
      Math.sqrt(
        Math.sin((lat2 - lat1) / 2) ** 2 +
          Math.cos(lat1) * Math.cos(lat2) * Math.sin((lng2 - lng1) / 2) ** 2,
      ),
    );
  if (d === 0) return [a, b];
  const out: LngLat[] = [];
  for (let i = 0; i <= steps; i++) {
    const f = i / steps;
    const A = Math.sin((1 - f) * d) / Math.sin(d);
    const B = Math.sin(f * d) / Math.sin(d);
    const x = A * Math.cos(lat1) * Math.cos(lng1) + B * Math.cos(lat2) * Math.cos(lng2);
    const y = A * Math.cos(lat1) * Math.sin(lng1) + B * Math.cos(lat2) * Math.sin(lng2);
    const z = A * Math.sin(lat1) + B * Math.sin(lat2);
    out.push([toDeg(Math.atan2(y, x)), toDeg(Math.atan2(z, Math.sqrt(x * x + y * y)))]);
  }
  out[0] = a;
  out[out.length - 1] = b;
  return out;
}

/**
 * A gently curved "flight arc": a quadratic Bezier bowed to the left of travel. It reads as a
 * flight on the map; it is illustrative and not a flight path.
 */
export function flightArc(a: LngLat, b: LngLat, steps = 48, bulge = 0.18): LngLat[] {
  const mid: LngLat = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
  const dx = b[0] - a[0];
  const dy = b[1] - a[1];
  const control: LngLat = [mid[0] - dy * bulge, mid[1] + dx * bulge];
  const out: LngLat[] = [];
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const u = 1 - t;
    out.push([
      u * u * a[0] + 2 * u * t * control[0] + t * t * b[0],
      u * u * a[1] + 2 * u * t * control[1] + t * t * b[1],
    ]);
  }
  return out;
}

/** Small deterministic hash so generated sea routes are stable between renders. */
export function hashString(value: string): number {
  let h = 2166136261;
  for (let i = 0; i < value.length; i++) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/**
 * A plausible-looking sea polyline between two points: a smooth S-curve with a deterministic
 * wobble seeded by `seed`. Boats do not sail straight lines, but we have no navigational data,
 * so this is illustrative only and callers must flag it `approximate`.
 */
export function seaRoute(a: LngLat, b: LngLat, seed: string, steps = 32): LngLat[] {
  const length = haversineKm(a, b);
  if (length < 0.05) return [a, b];
  const rnd = hashString(seed);
  const phase = ((rnd % 1000) / 1000) * Math.PI;
  const direction = rnd % 2 === 0 ? 1 : -1;
  const amplitude = Math.min(0.12, 0.045 + length / 1500) * length;
  const dx = b[0] - a[0];
  const dy = b[1] - a[1];
  const norm = Math.hypot(dx, dy) || 1;
  const nx = (-dy / norm) * direction;
  const ny = (dx / norm) * direction;
  const degPerKm = norm / length;
  const out: LngLat[] = [];
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const envelope = Math.sin(Math.PI * t);
    const offset = amplitude * degPerKm * envelope * Math.sin(2 * Math.PI * t + phase);
    out.push([a[0] + dx * t + nx * offset, a[1] + dy * t + ny * offset]);
  }
  out[0] = a;
  out[out.length - 1] = b;
  return out;
}
