import type {
  DataDrivenPropertyValueSpecification,
  ExpressionSpecification,
  LayerSpecification,
  StyleSpecification,
} from "maplibre-gl";
import { fontStacks, mapColors } from "../theme/tokens";

export const OMT_SOURCE = "omt";
export const LAND_SOURCE = "land";
export const REEF_SOURCE = "reefs";

export const ATTRIBUTION =
  '<a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">© OpenStreetMap contributors</a> · <a href="https://openmaptiles.org/" target="_blank" rel="noreferrer">© OpenMapTiles</a>';

export interface MapStyleOptions {
  /** Base URL serving maldives.pmtiles, land.geojson, reefs.geojson and fonts/. */
  baseUrl: string;
}

type ZoomStops = [number, number | string][];

const interpolate = (stops: ZoomStops): ExpressionSpecification =>
  ["interpolate", ["linear"], ["zoom"], ...stops.flat()] as ExpressionSpecification;

const zoomNumber = (stops: [number, number][]) =>
  interpolate(stops) as DataDrivenPropertyValueSpecification<number>;

const IN_CLASS = (cls: string[]): ExpressionSpecification => [
  "in",
  ["get", "class"],
  ["literal", cls],
];

/**
 * The Dhathuru basemap: deep-teal sea, soft turquoise shelves and reefs, warm-sand islands,
 * restrained labels. Layer ids are stable (see layer-ids.ts) because routes insert themselves
 * below the label layers.
 */
export function createMapStyle({ baseUrl }: MapStyleOptions): StyleSpecification {
  const layers: LayerSpecification[] = [
    {
      id: "sea",
      type: "background",
      paint: {
        "background-color": interpolate([
          [4, mapColors.seaDeep],
          [8, mapColors.seaMid],
          [12, mapColors.seaNear],
        ]) as unknown as string,
      },
    },
    // Shallow shelf: a wide blurred halo around every island, drawn under the island fill.
    {
      id: "shelf-wide",
      type: "line",
      source: LAND_SOURCE,
      minzoom: 6,
      layout: { "line-join": "round" },
      paint: {
        "line-color": mapColors.shelf,
        "line-opacity": interpolate([
          [6, 0.18],
          [10, 0.34],
          [14, 0.5],
        ]) as unknown as number,
        "line-width": zoomNumber([
          [6, 3],
          [10, 22],
          [14, 90],
        ]),
        "line-blur": zoomNumber([
          [6, 3],
          [10, 18],
          [14, 60],
        ]),
      },
    },
    {
      id: "shelf-near",
      type: "line",
      source: LAND_SOURCE,
      minzoom: 8,
      layout: { "line-join": "round" },
      paint: {
        "line-color": mapColors.lagoon,
        "line-opacity": 0.5,
        "line-width": zoomNumber([
          [8, 2],
          [12, 14],
          [16, 60],
        ]),
        "line-blur": zoomNumber([
          [8, 2],
          [12, 10],
          [16, 36],
        ]),
      },
    },
    // Reefs and lagoons.
    {
      id: "reef-fill",
      type: "fill",
      source: REEF_SOURCE,
      minzoom: 5,
      paint: {
        "fill-color": mapColors.lagoon,
        "fill-opacity": interpolate([
          [5, 0.55],
          [10, 0.7],
          [14, 0.8],
        ]) as unknown as number,
      },
    },
    {
      id: "reef-edge",
      type: "line",
      source: REEF_SOURCE,
      minzoom: 7,
      paint: {
        "line-color": mapColors.reefEdge,
        "line-opacity": 0.85,
        "line-width": zoomNumber([
          [7, 0.4],
          [11, 1.2],
          [15, 3],
        ]),
        "line-blur": 0.6,
      },
    },
    // Islands.
    {
      id: "land-fill",
      type: "fill",
      source: LAND_SOURCE,
      paint: { "fill-color": mapColors.land, "fill-antialias": true },
    },
    {
      id: "land-beach",
      type: "line",
      source: LAND_SOURCE,
      paint: {
        "line-color": mapColors.beach,
        "line-opacity": 0.95,
        "line-width": zoomNumber([
          [6, 0.5],
          [10, 1.6],
          [14, 4],
          [17, 9],
        ]),
        "line-blur": zoomNumber([
          [6, 0.2],
          [14, 1.4],
        ]),
      },
    },
    {
      id: "land-edge",
      type: "line",
      source: LAND_SOURCE,
      minzoom: 11,
      paint: {
        "line-color": mapColors.landEdge,
        "line-width": zoomNumber([
          [11, 0.4],
          [16, 1.2],
        ]),
      },
    },
    // Land cover and land use (only drawn on the islands because they sit on land).
    {
      id: "landcover-green",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "landcover",
      minzoom: 11,
      filter: IN_CLASS(["grass", "wood"]),
      paint: {
        "fill-color": [
          "match",
          ["get", "class"],
          "wood",
          mapColors.wood,
          mapColors.park,
        ] as ExpressionSpecification,
        "fill-opacity": 0.85,
      },
    },
    {
      id: "landuse-built",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "landuse",
      minzoom: 12,
      filter: IN_CLASS([
        "residential",
        "commercial",
        "industrial",
        "school",
        "hospital",
        "university",
        "college",
      ]),
      paint: { "fill-color": mapColors.built, "fill-opacity": 0.8 },
    },
    {
      id: "landuse-green",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "landuse",
      minzoom: 12,
      filter: IN_CLASS(["pitch", "playground", "stadium", "track", "cemetery"]),
      paint: { "fill-color": mapColors.park, "fill-opacity": 0.7 },
    },
    {
      id: "park",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "park",
      minzoom: 11,
      paint: { "fill-color": mapColors.park, "fill-opacity": 0.8 },
    },
    // Airports.
    {
      id: "aeroway-area",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "aeroway",
      minzoom: 9,
      filter: ["==", ["geometry-type"], "Polygon"],
      paint: { "fill-color": mapColors.runway, "fill-opacity": 0.9 },
    },
    {
      id: "runway",
      type: "line",
      source: OMT_SOURCE,
      "source-layer": "aeroway",
      minzoom: 9,
      filter: ["all", ["==", ["geometry-type"], "LineString"], IN_CLASS(["runway", "taxiway"])],
      paint: {
        "line-color": mapColors.runway,
        "line-width": zoomNumber([
          [9, 1.2],
          [13, 7],
          [16, 22],
        ]),
      },
    },
    {
      id: "buildings",
      type: "fill",
      source: OMT_SOURCE,
      "source-layer": "building",
      minzoom: 14,
      paint: {
        "fill-color": mapColors.buildings,
        "fill-opacity": 0.9,
        "fill-outline-color": mapColors.landEdge,
      },
    },
    // Roads: casing then fill, minor roads appear later.
    {
      id: "road-casing",
      type: "line",
      source: OMT_SOURCE,
      "source-layer": "transportation",
      minzoom: 12,
      filter: [
        "all",
        ["==", ["geometry-type"], "LineString"],
        IN_CLASS(["motorway", "trunk", "primary", "secondary", "tertiary", "minor", "service"]),
      ],
      layout: { "line-cap": "round", "line-join": "round" },
      paint: {
        "line-color": mapColors.roadCasing,
        "line-width": zoomNumber([
          [12, 1.4],
          [15, 5],
          [18, 18],
        ]),
      },
    },
    {
      id: "road",
      type: "line",
      source: OMT_SOURCE,
      "source-layer": "transportation",
      minzoom: 12,
      filter: [
        "all",
        ["==", ["geometry-type"], "LineString"],
        IN_CLASS(["motorway", "trunk", "primary", "secondary", "tertiary", "minor", "service"]),
      ],
      layout: { "line-cap": "round", "line-join": "round" },
      paint: {
        "line-color": mapColors.road,
        "line-width": zoomNumber([
          [12, 0.8],
          [15, 3.4],
          [18, 15],
        ]),
      },
    },
    {
      id: "path",
      type: "line",
      source: OMT_SOURCE,
      "source-layer": "transportation",
      minzoom: 15,
      filter: ["all", ["==", ["geometry-type"], "LineString"], IN_CLASS(["path", "pedestrian"])],
      paint: { "line-color": mapColors.roadCasing, "line-width": 1, "line-dasharray": [2, 2] },
    },
    // Labels. Routes are inserted below `place-island` so labels stay legible on top of them.
    {
      id: "place-island",
      type: "symbol",
      source: OMT_SOURCE,
      "source-layer": "place",
      minzoom: 8,
      filter: IN_CLASS(["island", "city", "town", "village", "hamlet"]),
      layout: {
        "text-field": ["coalesce", ["get", "name_en"], ["get", "name"]] as ExpressionSpecification,
        "text-font": [...fontStacks.regular],
        "text-size": zoomNumber([
          [8, 10],
          [11, 12],
          [14, 15],
        ]),
        "text-max-width": 7,
        "text-letter-spacing": 0.02,
        "symbol-sort-key": ["coalesce", ["get", "rank"], 100] as ExpressionSpecification,
        "text-anchor": "top",
        "text-offset": [0, 0.4],
        "text-padding": 3,
      },
      paint: {
        "text-color": mapColors.label,
        "text-halo-color": mapColors.labelHalo,
        "text-halo-width": 1.6,
        "text-halo-blur": 0.4,
      },
    },
    {
      id: "road-name",
      type: "symbol",
      source: OMT_SOURCE,
      "source-layer": "transportation_name",
      minzoom: 14,
      layout: {
        "symbol-placement": "line",
        "text-field": ["coalesce", ["get", "name_en"], ["get", "name"]] as ExpressionSpecification,
        "text-font": [...fontStacks.regular],
        "text-size": 11,
      },
      paint: {
        "text-color": mapColors.label,
        "text-halo-color": mapColors.labelHalo,
        "text-halo-width": 1.4,
      },
    },
    {
      id: "poi-ferry",
      type: "symbol",
      source: OMT_SOURCE,
      "source-layer": "poi",
      minzoom: 14,
      filter: IN_CLASS(["ferry_terminal", "harbor"]),
      layout: {
        "icon-image": "jetty",
        "icon-size": 0.9,
        "icon-allow-overlap": true,
        "text-field": ["coalesce", ["get", "name_en"], ["get", "name"]] as ExpressionSpecification,
        "text-font": [...fontStacks.regular],
        "text-size": 11,
        "text-anchor": "top",
        "text-offset": [0, 1.1],
        "text-optional": true,
      },
      paint: {
        "text-color": mapColors.label,
        "text-halo-color": mapColors.labelHalo,
        "text-halo-width": 1.4,
      },
    },
    {
      id: "aerodrome-label",
      type: "symbol",
      source: OMT_SOURCE,
      "source-layer": "aerodrome_label",
      minzoom: 7,
      layout: {
        "icon-image": "airport",
        "icon-size": 0.95,
        "icon-allow-overlap": true,
        "text-field": ["coalesce", ["get", "name_en"], ["get", "name"]] as ExpressionSpecification,
        "text-font": [...fontStacks.bold],
        "text-size": zoomNumber([
          [7, 10],
          [12, 12],
        ]),
        "text-anchor": "top",
        "text-offset": [0, 1.2],
        "text-max-width": 8,
        "text-optional": true,
      },
      paint: {
        "text-color": mapColors.label,
        "text-halo-color": mapColors.labelHalo,
        "text-halo-width": 1.6,
      },
    },
    {
      id: "sea-name",
      type: "symbol",
      source: OMT_SOURCE,
      "source-layer": "water_name",
      layout: {
        "text-field": ["coalesce", ["get", "name_en"], ["get", "name"]] as ExpressionSpecification,
        "text-font": [...fontStacks.italic],
        "text-size": zoomNumber([
          [4, 12],
          [9, 16],
        ]),
        "text-letter-spacing": 0.18,
        "text-max-width": 8,
      },
      paint: {
        "text-color": mapColors.seaLabel,
        "text-halo-color": mapColors.seaLabelHalo,
        "text-halo-width": 1.2,
        "text-opacity": 0.9,
      },
    },
  ];

  return {
    version: 8,
    name: "Dhathuru",
    glyphs: `${baseUrl}/fonts/{fontstack}/{range}.pbf`,
    sources: {
      [OMT_SOURCE]: {
        type: "vector",
        url: `pmtiles://${baseUrl}/maldives.pmtiles`,
        attribution: ATTRIBUTION,
      },
      [LAND_SOURCE]: { type: "geojson", data: `${baseUrl}/land.geojson`, tolerance: 0.2 },
      [REEF_SOURCE]: { type: "geojson", data: `${baseUrl}/reefs.geojson`, tolerance: 0.3 },
    },
    layers,
  };
}
