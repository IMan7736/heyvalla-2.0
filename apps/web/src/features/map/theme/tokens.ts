/**
 * Map palette. Brand colours mirror docs/PLAN.md section 9 and apps/web globals.css; the sea and
 * land ramps are map-only. LAGOON may re-export these from packages/ui later.
 */
export const brand = {
  lagoon: "#087F8C",
  sand: "#FAF7F0",
  coconut: "#FFFFFF",
  ocean: "#12343B",
  coral: "#F28C78",
} as const;

export const mapColors = {
  land: "#F4EBD6",
  landEdge: "#E6D9B8",
  beach: "#FFFDF6",
  park: "#DCE6C4",
  wood: "#CFDDB5",
  built: "#EADFC4",
  buildings: "#E0D3B4",
  road: "#FFFFFF",
  roadCasing: "#DACBA5",
  runway: "#D8CCAE",
  seaDeep: "#0B6571",
  seaMid: "#17808D",
  seaNear: "#2594A0",
  shelf: "#5EC4C3",
  lagoon: "#9FE0D8",
  lagoonLight: "#C9F0EA",
  reefEdge: "#E3F8F4",
  label: brand.ocean,
  labelHalo: "#F7F1E1",
  seaLabel: "#CFF1EE",
  seaLabelHalo: "#0B5E69",
  route: {
    sea: brand.lagoon,
    air: brand.ocean,
    road: brand.ocean,
    walk: brand.ocean,
    accent: brand.coral,
    casing: "#FFFFFF",
  },
} as const;

export const fontStacks = {
  regular: ["Noto Sans Regular"],
  bold: ["Noto Sans Bold"],
  italic: ["Noto Sans Italic"],
} as const;
