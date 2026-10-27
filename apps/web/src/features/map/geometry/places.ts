import type { FeatureCollection, Point } from "geojson";
import type { Place } from "../types";

const fold = (value: string) =>
  value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();

/**
 * Name search for the island pickers: diacritics and case are ignored (Malé matches "male"),
 * prefix matches rank above substring matches, hubs and airports break ties.
 */
export function searchPlaces(places: readonly Place[], query: string, limit = 8): Place[] {
  const q = fold(query.trim());
  if (!q) return [];
  const scored: { place: Place; score: number }[] = [];
  for (const place of places) {
    const name = fold(place.name.en);
    const code = place.code ? fold(place.code) : "";
    let score = -1;
    if (name.startsWith(q) || code === q) score = 3;
    else if (name.split(/[\s-]+/).some((w) => w.startsWith(q))) score = 2;
    else if (name.includes(q)) score = 1;
    if (score < 0) continue;
    scored.push({ place, score: score * 2 + (place.isHub ? 1 : 0) });
  }
  return scored
    .sort((a, b) => b.score - a.score || a.place.name.en.localeCompare(b.place.name.en))
    .slice(0, limit)
    .map((s) => s.place);
}

export function placesToFeatures(
  places: readonly Place[],
  serviceCounts: Readonly<Record<string, number>> = {},
): FeatureCollection<Point> {
  return {
    type: "FeatureCollection",
    features: places.map((p) => ({
      type: "Feature",
      id: undefined,
      properties: {
        id: p.id,
        name: p.name.en,
        type: p.type,
        hub: p.isHub,
        services: serviceCounts[p.id] ?? 0,
      },
      geometry: { type: "Point", coordinates: [p.geo.lng, p.geo.lat] },
    })),
  };
}
