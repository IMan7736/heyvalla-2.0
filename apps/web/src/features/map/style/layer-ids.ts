/**
 * Layer registry. Everything the map feature adds to MapLibre is named here so that other
 * features (and tests) can refer to layers without string-guessing, and so routes can be
 * inserted below the label layers of the basemap.
 */
export const BASEMAP_LABEL_LAYER = "place-island";

export const ROUTE_SOURCES = {
  ahead: "dh-route-ahead",
  done: "dh-route-done",
  focus: "dh-route-focus",
} as const;

export const ROUTE_LAYERS = {
  aheadCasing: "dh-route-ahead-casing",
  ahead: "dh-route-ahead-line",
  doneCasing: "dh-route-done-casing",
  done: "dh-route-done-line",
  focus: "dh-route-focus-line",
} as const;

export const PLACE_SOURCE = "dh-places";
export const PLACE_LAYERS = {
  halo: "dh-places-density",
  clusters: "dh-places-clusters",
  clusterCount: "dh-places-cluster-count",
  points: "dh-places-points",
  labels: "dh-places-labels",
  selected: "dh-places-selected",
} as const;

export const NETWORK_SOURCE = "dh-network";
export const NETWORK_LAYERS = {
  casing: "dh-network-casing",
  line: "dh-network-line",
} as const;

export const layerRegistry = {
  basemapLabel: BASEMAP_LABEL_LAYER,
  routeSources: ROUTE_SOURCES,
  routeLayers: ROUTE_LAYERS,
  placeSource: PLACE_SOURCE,
  placeLayers: PLACE_LAYERS,
  networkSource: NETWORK_SOURCE,
  networkLayers: NETWORK_LAYERS,
} as const;
