import type { Feature, FeatureCollection } from "geojson";
import type {
  FilterSpecification,
  GeoJSONSource,
  LayerSpecification,
  Map as MapLibreMap,
} from "maplibre-gl";
import type { Route } from "../geometry/route";
import type { LngLat, ModeGroup } from "../types";
import { brand, mapColors } from "../theme/tokens";
import { BASEMAP_LABEL_LAYER, ROUTE_LAYERS, ROUTE_SOURCES } from "./layer-ids";

type RouteSourceKey = keyof typeof ROUTE_SOURCES;
export type LegLines = readonly (readonly LngLat[])[];

const emptyCollection: FeatureCollection = { type: "FeatureCollection", features: [] };

/** Features for the given per-leg coordinate lists; legs with fewer than 2 points are skipped. */
export function legFeatures(
  route: Route,
  lines: LegLines,
  onlyLegId?: string | null,
): FeatureCollection {
  const features: Feature[] = [];
  route.legs.forEach((leg, i) => {
    const coordinates = lines[i];
    if (!coordinates || coordinates.length < 2) return;
    if (onlyLegId && leg.id !== onlyLegId) return;
    features.push({
      type: "Feature",
      properties: { legId: leg.id, group: leg.group, mode: leg.mode, approximate: leg.approximate },
      geometry: { type: "LineString", coordinates: coordinates.map((c) => [...c]) },
    });
  });
  return { type: "FeatureCollection", features };
}

const GROUPS: ModeGroup[] = ["sea", "air", "road", "walk"];

interface GroupStyle {
  casingWidth: number;
  width: number;
  color: string;
  casingColor: string;
  casingOpacity: number;
  dasharray?: number[];
  cap: "round" | "butt";
}

const GROUP_STYLE: Record<ModeGroup, GroupStyle> = {
  // Ferries and boats: a translucent lane with white dashes, readable over deep and shallow water.
  sea: {
    casingWidth: 8,
    width: 3.2,
    color: "#FFFFFF",
    casingColor: "#073F49",
    casingOpacity: 0.45,
    dasharray: [1.3, 1.5],
    cap: "butt",
  },
  // Flights: a coral arc.
  air: {
    casingWidth: 6.5,
    width: 2.6,
    color: mapColors.route.accent,
    casingColor: brand.ocean,
    casingOpacity: 0.55,
    cap: "round",
  },
  // Taxis and buses follow roads.
  road: {
    casingWidth: 8,
    width: 4,
    color: brand.ocean,
    casingColor: "#FFFFFF",
    casingOpacity: 0.95,
    cap: "round",
  },
  walk: {
    casingWidth: 6,
    width: 2.6,
    color: brand.ocean,
    casingColor: "#FFFFFF",
    casingOpacity: 0.95,
    dasharray: [0.1, 2],
    cap: "round",
  },
};

const groupId = (base: string, group: ModeGroup) => `${base}-${group}`;

function lineLayers(
  source: string,
  casingBase: string,
  lineBase: string,
  emphasis: number,
  opacity: number,
): LayerSpecification[] {
  return GROUPS.flatMap((group): LayerSpecification[] => {
    const g = GROUP_STYLE[group];
    const filter: FilterSpecification = ["==", ["get", "group"], group];
    const layout = { "line-cap": g.cap, "line-join": "round" } as const;
    return [
      {
        id: groupId(casingBase, group),
        type: "line",
        source,
        filter,
        layout,
        paint: {
          "line-color": g.casingColor,
          "line-width": g.casingWidth * emphasis,
          "line-opacity": g.casingOpacity * opacity,
        },
      },
      {
        id: groupId(lineBase, group),
        type: "line",
        source,
        filter,
        layout,
        paint: {
          "line-color": g.color,
          "line-width": g.width * emphasis,
          "line-opacity": opacity,
          ...(g.dasharray ? { "line-dasharray": g.dasharray } : {}),
        },
      },
    ];
  });
}

export const routeLayerIds = (): string[] => [
  ...GROUPS.flatMap((g) => [
    groupId(ROUTE_LAYERS.aheadCasing, g),
    groupId(ROUTE_LAYERS.ahead, g),
    groupId(ROUTE_LAYERS.doneCasing, g),
    groupId(ROUTE_LAYERS.done, g),
  ]),
  ROUTE_LAYERS.focus,
];

/** Adds the route sources and layers below the basemap labels. Idempotent. */
export function addRouteLayers(map: MapLibreMap): void {
  if (map.getSource(ROUTE_SOURCES.ahead)) return;
  for (const key of Object.keys(ROUTE_SOURCES) as RouteSourceKey[]) {
    map.addSource(ROUTE_SOURCES[key], { type: "geojson", data: emptyCollection });
  }
  const beforeId = map.getLayer(BASEMAP_LABEL_LAYER) ? BASEMAP_LABEL_LAYER : undefined;
  const layers: LayerSpecification[] = [
    {
      id: ROUTE_LAYERS.focus,
      type: "line",
      source: ROUTE_SOURCES.focus,
      layout: { "line-cap": "round", "line-join": "round" },
      paint: {
        "line-color": mapColors.route.accent,
        "line-width": 16,
        "line-opacity": 0.35,
        "line-blur": 6,
      },
    },
    ...lineLayers(ROUTE_SOURCES.ahead, ROUTE_LAYERS.aheadCasing, ROUTE_LAYERS.ahead, 1, 0.55),
    ...lineLayers(ROUTE_SOURCES.done, ROUTE_LAYERS.doneCasing, ROUTE_LAYERS.done, 1.15, 1),
  ];
  for (const layer of layers) map.addLayer(layer, beforeId);
}

export function removeRouteLayers(map: MapLibreMap): void {
  if (!map.getStyle()) return;
  for (const id of routeLayerIds()) if (map.getLayer(id)) map.removeLayer(id);
  for (const id of Object.values(ROUTE_SOURCES)) if (map.getSource(id)) map.removeSource(id);
}

export function setRouteSource(
  map: MapLibreMap,
  key: RouteSourceKey,
  data: FeatureCollection,
): void {
  (map.getSource(ROUTE_SOURCES[key]) as GeoJSONSource | undefined)?.setData(data);
}
