import type {
  ExpressionSpecification,
  GeoJSONSource,
  GeoJSONSourceSpecification,
  Map as MapLibreMap,
} from "maplibre-gl";
import type { FeatureCollection, Point } from "geojson";
import type { ModeGroup } from "../types";
import { brand, mapColors } from "../theme/tokens";
import { fontStacks } from "../theme/tokens";
import {
  BASEMAP_LABEL_LAYER,
  NETWORK_LAYERS,
  NETWORK_SOURCE,
  PLACE_LAYERS,
  PLACE_SOURCE,
} from "./layer-ids";

export interface NetworkEdge {
  fromId: string;
  toId: string;
  group: ModeGroup;
}

/** Adds clustered place points, density halos, labels and a selection ring. Idempotent. */
export function addPlaceLayers(
  map: MapLibreMap,
  data: FeatureCollection<Point>,
  cluster: boolean,
): void {
  if (map.getSource(PLACE_SOURCE)) return;
  const spec: GeoJSONSourceSpecification = {
    type: "geojson",
    data,
    ...(cluster ? { cluster: true, clusterRadius: 44, clusterMaxZoom: 9 } : {}),
  };
  map.addSource(PLACE_SOURCE, spec);
  map.addSource(NETWORK_SOURCE, {
    type: "geojson",
    data: { type: "FeatureCollection", features: [] },
  });
  const before = map.getLayer(BASEMAP_LABEL_LAYER) ? BASEMAP_LABEL_LAYER : undefined;
  const single: ExpressionSpecification = ["!", ["has", "point_count"]];

  map.addLayer(
    {
      id: NETWORK_LAYERS.casing,
      type: "line",
      source: NETWORK_SOURCE,
      layout: { "line-cap": "round" },
      paint: { "line-color": "#073F49", "line-opacity": 0.3, "line-width": 5 },
    },
    before,
  );
  map.addLayer(
    {
      id: NETWORK_LAYERS.line,
      type: "line",
      source: NETWORK_SOURCE,
      layout: { "line-cap": "round" },
      paint: {
        "line-color": ["match", ["get", "group"], "air", mapColors.route.accent, "#FFFFFF"],
        "line-width": 1.6,
        "line-opacity": 0.8,
        "line-dasharray": [2, 2],
      },
    },
    before,
  );
  map.addLayer(
    {
      id: PLACE_LAYERS.halo,
      type: "circle",
      source: PLACE_SOURCE,
      filter: ["all", single, [">", ["get", "services"], 0]],
      paint: {
        "circle-color": mapColors.lagoonLight,
        "circle-opacity": 0.35,
        "circle-blur": 0.8,
        "circle-radius": ["interpolate", ["linear"], ["get", "services"], 1, 12, 20, 24, 100, 38],
      },
    },
    before,
  );
  map.addLayer(
    {
      id: PLACE_LAYERS.clusters,
      type: "circle",
      source: PLACE_SOURCE,
      filter: ["has", "point_count"],
      paint: {
        "circle-color": brand.lagoon,
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2.5,
        "circle-radius": ["step", ["get", "point_count"], 15, 10, 19, 30, 24],
      },
    },
    before,
  );
  map.addLayer(
    {
      id: PLACE_LAYERS.clusterCount,
      type: "symbol",
      source: PLACE_SOURCE,
      filter: ["has", "point_count"],
      layout: {
        "text-field": ["get", "point_count_abbreviated"],
        "text-font": [...fontStacks.bold],
        "text-size": 12,
        "text-allow-overlap": true,
      },
      paint: { "text-color": "#FFFFFF" },
    },
    before,
  );
  map.addLayer(
    {
      id: PLACE_LAYERS.points,
      type: "circle",
      source: PLACE_SOURCE,
      filter: single,
      paint: {
        "circle-color": [
          "match",
          ["get", "type"],
          "airport",
          brand.ocean,
          "jetty",
          brand.lagoon,
          brand.coconut,
        ],
        "circle-stroke-color": ["case", ["==", ["get", "hub"], true], brand.coral, brand.ocean],
        "circle-stroke-width": ["case", ["==", ["get", "hub"], true], 3, 1.5],
        "circle-radius": ["interpolate", ["linear"], ["zoom"], 5, 4, 10, 7, 14, 10],
      },
    },
    before,
  );
  map.addLayer(
    {
      id: PLACE_LAYERS.selected,
      type: "circle",
      source: PLACE_SOURCE,
      filter: ["==", ["get", "id"], ""],
      paint: {
        "circle-color": "rgba(0,0,0,0)",
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 4,
        "circle-radius": ["interpolate", ["linear"], ["zoom"], 5, 9, 14, 17],
      },
    },
    before,
  );
  map.addLayer({
    id: PLACE_LAYERS.labels,
    type: "symbol",
    source: PLACE_SOURCE,
    minzoom: 8,
    filter: single,
    layout: {
      "text-field": ["get", "name"],
      "text-font": [...fontStacks.bold],
      "text-size": 12,
      "text-anchor": "top",
      "text-offset": [0, 0.9],
      "text-optional": true,
    },
    paint: {
      "text-color": mapColors.label,
      "text-halo-color": mapColors.labelHalo,
      "text-halo-width": 1.6,
    },
  });
}

export function removePlaceLayers(map: MapLibreMap): void {
  if (!map.getStyle()) return;
  for (const id of [...Object.values(PLACE_LAYERS), ...Object.values(NETWORK_LAYERS)])
    if (map.getLayer(id)) map.removeLayer(id);
  for (const id of [PLACE_SOURCE, NETWORK_SOURCE]) if (map.getSource(id)) map.removeSource(id);
}

export function setPlaceData(map: MapLibreMap, data: FeatureCollection<Point>): void {
  (map.getSource(PLACE_SOURCE) as GeoJSONSource | undefined)?.setData(data);
}

export function setSelectedPlace(map: MapLibreMap, id: string | null): void {
  if (map.getLayer(PLACE_LAYERS.selected))
    map.setFilter(PLACE_LAYERS.selected, ["==", ["get", "id"], id ?? ""]);
}

export function setNetworkEdges(
  map: MapLibreMap,
  edges: readonly NetworkEdge[],
  coords: ReadonlyMap<string, [number, number]>,
  enabled: ReadonlySet<ModeGroup>,
): void {
  const features = edges.flatMap((e) => {
    const a = coords.get(e.fromId);
    const b = coords.get(e.toId);
    if (!a || !b || !enabled.has(e.group)) return [];
    return [
      {
        type: "Feature" as const,
        properties: { group: e.group },
        geometry: { type: "LineString" as const, coordinates: [a, b] },
      },
    ];
  });
  (map.getSource(NETWORK_SOURCE) as GeoJSONSource | undefined)?.setData({
    type: "FeatureCollection",
    features,
  });
}
