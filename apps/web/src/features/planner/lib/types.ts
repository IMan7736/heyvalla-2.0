import type { components } from "@dhathuru/contracts";

type Schemas = components["schemas"];

export type TripOption = Schemas["TripOption"];
export type Leg = Schemas["Leg"];
export type Place = Schemas["Place"];
export type PlaceRef = Schemas["PlaceRef"];
export type Money = Schemas["Money"];
export type TransportMode = Schemas["TransportMode"];
export type TripLabel = Schemas["TripLabel"];
export type RiskLevel = Schemas["RiskLevel"];
export type Buffer = Schemas["Buffer"];
export type Explanation = Schemas["Explanation"];
export type Warning = Schemas["Warning"];
export type LegFeatureCollection = Schemas["LegFeatureCollection"];
export type TripSearchRequest = Schemas["TripSearchRequest"];
export type TripSearchResponse = Schemas["TripSearchResponse"];

export const TRANSPORT_MODES: readonly TransportMode[] = [
  "ferry",
  "speedboat",
  "seaplane",
  "domestic_flight",
  "bus",
  "taxi",
  "walk",
  "resort_transfer",
];
