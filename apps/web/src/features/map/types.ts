import type { components } from "@dhathuru/contracts";

export type TripOption = components["schemas"]["TripOption"];
export type Leg = components["schemas"]["Leg"];
export type PlaceRef = components["schemas"]["PlaceRef"];
export type Place = components["schemas"]["Place"];
export type TransportMode = components["schemas"]["TransportMode"];
export type LegFeatureCollection = components["schemas"]["LegFeatureCollection"];

/** [longitude, latitude] in WGS84, the GeoJSON order. */
export type LngLat = [lng: number, lat: number];

/** West/south/east/north corners, the shape MapLibre `fitBounds` takes. */
export type Bounds = [sw: LngLat, ne: LngLat];

/** How a leg is drawn. Several modes share a visual language. */
export type ModeGroup = "sea" | "air" | "road" | "walk";
