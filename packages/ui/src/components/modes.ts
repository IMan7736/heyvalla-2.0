import type { IconName } from "../icons/icons";

/** Service/leg modes from docs/CONTRACT.md. */
export type TransportMode =
  | "ferry"
  | "speedboat"
  | "seaplane"
  | "domestic_flight"
  | "bus"
  | "taxi"
  | "walk"
  | "resort_transfer";

export const modeIcon: Record<TransportMode, IconName> = {
  ferry: "ferry",
  speedboat: "speedboat",
  seaplane: "seaplane",
  domestic_flight: "plane",
  bus: "bus",
  taxi: "taxi",
  walk: "walk",
  resort_transfer: "resort-transfer",
};

export type ModeFamily = "sea" | "air" | "land";
export const modeFamily: Record<TransportMode, ModeFamily> = {
  ferry: "sea",
  speedboat: "sea",
  resort_transfer: "sea",
  seaplane: "air",
  domestic_flight: "air",
  bus: "land",
  taxi: "land",
  walk: "land",
};

/** Tonal chip colours per family. Distinguished by icon + label first; tone is a secondary cue. */
export const familyChip: Record<ModeFamily, string> = {
  sea: "bg-lagoon-100 text-lagoon-800",
  air: "bg-ocean-900 text-ink-inverse",
  land: "bg-sand-300 text-ocean-900",
};
